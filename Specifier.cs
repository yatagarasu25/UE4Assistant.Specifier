using System.Globalization;
using SystemEx;

namespace UE4Assistant
{
	public record struct Specifier(string type = null, Dictionary<string, object> data = null, int startIndex = 0, int endIndex = 0)
	{
		private static Dictionary<string, object> NewData => new Dictionary<string, object>(StringComparer.InvariantCultureIgnoreCase);

		public bool IsEmpty => type.IsNullOrWhiteSpace();

		Lazy<TagModel> tag_ = null;
		public TagModel tag {
			get {
				var ltype_ = type.ToLower();
				tag_ = tag_.Lazy(() => SpecifierSchema.ReadAvailableTags().Where(t => t.name == ltype_).FirstOrDefault());
				return tag_.Value;
			}
		}

		public Lazy<SpecifierModel> model_ = null;
		public SpecifierModel model {
			get {
				var ltag_ = tag;
				model_ = model_.Lazy(() => SpecifierSchema.ReadSpecifierModel(ltag_.name));
				return model_.Value;
			}
		}

		public Dictionary<string, object> GetData(string name)
			=> data.TryGetValue(name, out var root) ? (Dictionary<string, object>)root : null;

		public override string ToString()
			=> $"{type}({GenerateSpecifier(SpecifierSchema.ReadSpecifierSettings(type))})";

		// create groups of one value or list all flags to group
		public IEnumerable<IGrouping<string, SpecifierParameterModel>> GroupProperties(string name)
			=> model.collections[name == "" ? "parameters" : name].GroupBy(p => p.group.IsNullOrWhiteSpace() ? p.name : p.group);


		public static IEnumerable<(int si, int ei, Specifier s)> FindAll(string line)
		{
			int li = 0;
			while (true)
			{
				li = line.IndexOf('(', li);
				if (li < 0)
					break;

				var si = line.IndexOfAnyReverse(li, ' ', '\t', ',', ';', '*', '&') + 1;

				var tokenizer = line.tokenize(si);
				if (TryParse(tokenizer, out var specifier))
				{
					yield return (si, tokenizer.ei, specifier);
					li = tokenizer.li + 1;
				}

				if (li >= line.Length)
					break;
			}
		}

		public static bool TryParse(string str, out Specifier s) => TryParse(str.tokenize(), out s);
		public static bool TryParse(LineTokenizer tokenizer, out Specifier s)
		{
			s = new();

			bool ok = tokenizer.find_any(out char ch, "()=,\"");
			if (!ok || ch != '(')
			{
				return false;
			}

			try
			{
				string name = tokenizer.token.Trim();
				s = new(name, ParseSpecifierData(tokenizer).Aggregate(NewData, (s, i) => s.Also(_ => _.Add(i.name, i.Item2))));

				return true;
			}
			catch
			{
				ok &= tokenizer.find_any(')');
			}

			return false;
		}

		public Specifier Swap(string name, Dictionary<string, object> newData)
			=> new Specifier(type, data.ToDictionary(i => i.Key, i => i.Key == name ? newData : i.Value));

		static IEnumerable<(string name, Dictionary<string, object>)> ParseSpecifierData(LineTokenizer tokenizer)
		{
			Dictionary<string, object> result = NewData;

			bool ok = tokenizer.find_any(out char ch, '(');
			if (ok)
				tokenizer.step();

			while (ok)
			{
				string name = null;

				ok &= tokenizer.skip_whitespace();
				ok &= tokenizer.find_any(out ch, '=', ',', '(', ')', '"');
				name = tokenizer.token.Trim();

				if (!ok) // name
				{
					if (!name.IsNullOrWhiteSpace())
						try { result.Add(name, null); } catch { }

					yield return ("", result);
				}

				if (name.IsNullOrWhiteSpace()) // name is empty
				{
					ok &= tokenizer.step();
				}
				else if (ch == ')') // name)
				{
					result.Add(name, null);
				}
				else if (ch == ',') // name,
				{
					result.Add(name, null);
					tokenizer.step();
				}
				else if (ch == '=') // name=...
				{
					tokenizer.step();
					if (!tokenizer.skip_whitespace(out ch) || ch == ')') // name= or name=)
					{
						result.Add(name, string.Empty);
						break;
					}
					else if (ch == '"') // name="....
					{
						tokenizer.step();
						if (!tokenizer.find_any(out ch, '"')) // name="value
						{
							result.Add(name, tokenizer.token);
							break;
						}

						// name="value"
						result.Add(name, tokenizer.token);
						tokenizer.step();
					}
					else if (ch == '(') // name=(...
					{
						foreach (var d in ParseSpecifierData(tokenizer))
							yield return (name + d.name, d.Item2);
					}
					else // name=value
					{
						if (!tokenizer.find_any(out ch, '=', ',', '(', ')', '"'))
						{
							result.Add(name, tokenizer.token.Trim());
							break;
						}

						result.Add(name, tokenizer.token.Trim()
							.ToAnyType(typeof(bool), typeof(int), typeof(float)));
						if (ch != ')')
							tokenizer.step();
					}
				}
				else if (ch == '"') // name"...
				{
					tokenizer.step();
					if (!tokenizer.find_any('"')) // name"value
					{
						result.Add(name, tokenizer.token);
						break;
					}

					// name"value"
					result.Add(name, tokenizer.token);
					tokenizer.step();
				}
				else if (ch == '(') // name(...
				{
					foreach (var d in ParseSpecifierData(tokenizer))
						yield return (name + d.name, d.Item2);
				}

				ok &= tokenizer.skip_whitespace(out ch);
				if (ok)
				{
					if (ch == ')') // object end.
					{
						ok &= tokenizer.step();
						break;
					}
					else if (ch == ',')
						ok &= tokenizer.step();
				}
			}

			yield return ("", result);
		}

		static IEnumerable<string> GenerateSpecifierTokens(Dictionary<string, object> data, SpecifierSettings specifierSettings)
		{
			var keys = data.Keys.ToList();
			keys.Sort((string a, string b) => {
				int o = specifierSettings.GetParameterOrder(a).CompareTo(specifierSettings.GetParameterOrder(b));
				return o == 0 ? a.CompareTo(b) : o;
			});

			foreach (var key in keys)
			{
				var value = data[key];

				if (value != null)
				{
					if (value is string str)
					{
						yield return @$"{key} = ""{str}""";
					}
					else
					{
						if (value is bool b)
						{
							yield return @$"{key} = {(b ? "true" : "false")}";
						}
						else
						{
							yield return @$"{key} = {Convert.ToString(value, CultureInfo.InvariantCulture)}";
						}
					}
				}
				else
				{
					yield return key;
				}
			}
		}

		string GenerateSpecifier(SpecifierSettings specifierSettings)
			=> this.Let(this_
				=> this_.data.ToDictionary(i => i.Key, i => GenerateSpecifierTokens(this_.GetData(i.Key), specifierSettings).Join(", "))
					.OrderBy(i => i.Key)
					.Select(i => i.Key.IsNullOrWhiteSpace() ? i.Value : $"{i.Key} = ({i.Value})")
					.Join(", "));
	}
}
