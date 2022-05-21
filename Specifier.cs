using SystemEx;

namespace UE4Assistant
{
	public record struct Specifier(string type = null, Dictionary<string, object> data = null, int startIndex = 0, int endIndex = 0)
	{
		Lazy<TagModel> TagModel_ = new Lazy<TagModel>();
		public TagModel tag {
			get {
				var type_ = type;
				TagModel_ = TagModel_.Lazy(() => SpecifierSchema.ReadAvailableTags().Where(t => t.IsMatch(type_)).FirstOrDefault());
				return TagModel_.Value;
			}
		}
		/*
		public override string ToString()
		{
			return type + data.GenerateSpecifier(SpecifierSchema.ReadSpecifierSettings(type));
		}
		*/

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
				var specifierData = new Dictionary<string, object>();// tokenizer.ParseSpecifier();
				if (specifierData != null && specifierData.Keys.Count == 1)
				{
					var specifierType = specifierData.Keys.First();
					Specifier specifier = new Specifier(
						type: specifierType,
						data: specifierData[specifierType] as Dictionary<string, object>);

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

		public static Dictionary<string, object> ParseSpecifierData(LineTokenizer tokenizer)
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
	}

}
