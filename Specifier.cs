namespace UE4Assistant;

public record struct Specifier(string type = null, Dictionary<string, object> data = null)
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

	public static Specifier Create(TagModel t)
		=> new Specifier(t.name.ToUpper())
			.Let(s =>
				new Specifier(t.name.ToUpper(),
					s.model.collections.Aggregate(NewData, (a, i) => a.Also(_ => _.Add(i.Key == "parameters" ? "" : i.Key, NewData)))));

	public Dictionary<string, object> GetData(string name)
		=> data.TryGetValue(name, out var root) ? (Dictionary<string, object>)root : null;

	public Specifier Swap(string name, Dictionary<string, object> newData)
		=> new Specifier(type, data.ToDictionary(i => i.Key, i => i.Key == name ? newData : i.Value));

	// create groups of one value or list all flags to group
	public IEnumerable<IGrouping<string, SpecifierParameterModel>> GroupProperties(string name)
		=> model.collections[name == "" ? "parameters" : name].GroupBy(p => p.group.IsNullOrWhiteSpace() ? p.name : p.group);


	public static IEnumerable<(int si, int ei, Specifier s)> FindAll(string line, int caret_index = 0)
	{
		int li = caret_index;
		while (true)
		{
			li = line.IndexOfAny(li, '(', ')');
			if (li < 0)
				break;
			if (line[li] == ')')
				li = line.IndexOfAnyReverse(li, '(');
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


	public override string ToString() => $"{type}({GenerateSpecifier(SpecifierSchema.ReadSpecifierSettings(type))})";
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
			var li = tokenizer.li;
			string name = tokenizer.token.Trim();
			s = new(name, ParseSpecifierData(tokenizer).Aggregate(NewData, (s, i) => s.Also(_ => _.Add(i.name, i.Item2))));
			var ei = tokenizer.ei;

			return true;
		}
		catch
		{
			ok &= tokenizer.find_any(')');
		}

		return false;
	}

	static IEnumerable<(string name, Dictionary<string, object>)> ParseSpecifierData(LineTokenizer tokenizer)
	{
		const string tokenTerminators = "=,()\"";

		Dictionary<string, object> result = NewData;

		bool ok = tokenizer.find_any(out char ch, '(');
		if (ok)
			tokenizer.step();

		while (ok)
		{
			string name = null;

			ok &= tokenizer.skip_whitespace();
			ok &= tokenizer.find_any(out ch, tokenTerminators);
			name = tokenizer.token.Trim();

			if (!ok) // name
			{
				if (!name.IsNullOrWhiteSpace())
					try { result.Add(name, null); } catch { }

				yield return ("", result);
			}

			object true_ = (object)true;
			object false_ = (object)false;

			if (name.IsNullOrWhiteSpace()) // name is empty
			{
				ok &= tokenizer.step();
			}
			else
			{
				var r = ch switch {
					')' => true_.Also(_ => result.Add(name, null)),
					',' => true_.Also(_ => {
						result.Add(name, null);
						tokenizer.step();
					}),
					'=' => true_.Let(_ => {
						tokenizer.step();
						if (!tokenizer.skip_whitespace(out ch) || ch == ')') // name= or name=)
						{
							result.Add(name, string.Empty);
							return false_;
						}
						else return ch switch {
							'"' => true_.Let(_ => {
								tokenizer.step();
								if (!tokenizer.find_any(out ch, '"')) // name="value
								{
									result.Add(name, tokenizer.token);
									return false_;
								}

								// name="value"
								result.Add(name, tokenizer.token);
								tokenizer.step();

								return _;
							}),
							'(' => ParseSpecifierData(tokenizer),
							_ => true_.Let(_ => {
								if (!tokenizer.find_any(out ch, tokenTerminators))
								{
									result.Add(name, tokenizer.token.Trim());
									return false_;
								}

								result.Add(name, tokenizer.token.Trim()
									.ToAnyType(typeof(bool), typeof(int), typeof(float)));
								if (ch != ')')
									tokenizer.step();

								return _;
							})
						};
					}),
					'"' => true_.Let(_ => {
						tokenizer.step();
						if (!tokenizer.find_any('"')) // name"value
						{
							result.Add(name, tokenizer.token);
							return false_;
						}

						// name"value"
						result.Add(name, tokenizer.token);
						tokenizer.step();

						return _;
					}),
					'(' => ParseSpecifierData(tokenizer)
				};
				if (r is IEnumerable<(string name, Dictionary<string, object>)> de)
				{
					foreach (var d in de)
						yield return (name + d.name, d.Item2);
				}
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
				.Where(i => !i.Value.IsNullOrWhiteSpace())
				.OrderBy(i => i.Key)
				.Select(i => i.Key.IsNullOrWhiteSpace() ? i.Value : $"{i.Key} = ({i.Value})")
				.Join(", "));
}
