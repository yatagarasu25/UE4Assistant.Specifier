﻿using System.Globalization;
using SystemEx;

namespace UE4Assistant
{
	public record struct Specifier(string type = null, Dictionary<string, object> data = null, int startIndex = 0, int endIndex = 0)
	{
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

		public override string ToString()
		{
			return type + GenerateSpecifier(data, SpecifierSchema.ReadSpecifierSettings(type));
		}

		public Dictionary<string, SpecifierParameterModel[]> ToModelDictionary(string name)
		{
			var groups = model.collections[name]
				.GroupBy(p => p.group
					, LambdaComparer.Create((string a, string b) 
						=> (a.IsNullOrWhiteSpace() || b.IsNullOrWhiteSpace()) 
						? -1 : string.Compare(a, b)
					));
			var items = groups.ToDictionary(g => g.Key.IsNullOrWhiteSpace() ? g.First().name : g.Key, g => g.ToArray());
			return items;
		}


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

		public static bool TryParse(LineTokenizer tokenizer, out Specifier s)
		{
			s = new ();

			bool ok = tokenizer.find_any(out char ch, "()=,\"");
			if (!ok || ch != '(')
			{
				return false;
			}

			try
			{
				string name = tokenizer.token.Trim();
				s = new (name, ParseSpecifierData(tokenizer));

				return true;
			}
			catch
			{
				ok &= tokenizer.find_any(')');
			}

			return false;
		}

		static Dictionary<string, object> ParseSpecifierData(LineTokenizer tokenizer)
		{
			Dictionary<string, object> result = new Dictionary<string, object>(StringComparer.InvariantCultureIgnoreCase);

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

					return result;
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
						result.Add(name, ParseSpecifierData(tokenizer));
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
					result.Add(name, ParseSpecifierData(tokenizer));
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

			return result;
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
					else if (value is Dictionary<string, object> dict)
					{
						yield return @$"{key} = {GenerateSpecifier(dict, specifierSettings)}";
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

		static string GenerateSpecifier(Dictionary<string, object> data, SpecifierSettings specifierSettings)
		{
			return $"({GenerateSpecifierTokens(data, specifierSettings).Join(", ")})";
		}
	}
}
