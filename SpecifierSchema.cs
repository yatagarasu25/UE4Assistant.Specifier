using Newtonsoft.Json;
using SystemEx;


#if NET472_OR_GREATER
namespace System.Runtime.CompilerServices { internal class IsExternalInit { } }
#endif


namespace UE4Assistant
{
	public class SpecifierSchema
	{
		private static string ReadSchemaFile(string filename)
		{
			try
			{
				var stream = typeof(SpecifierSchema).Assembly.GetManifestResourceStream($"UE4Assistant.Schema.{filename}.json");
				return new StreamReader(stream).ReadToEnd();
			}
			catch
			{
				return string.Empty;
			}
		}

		private static Dictionary<string, int> categories_ = null;
		public static Dictionary<string, int> ReadAvaliableCategories()
		{
			if (categories_ == null)
			{
				categories_ = JsonConvert.DeserializeObject<List<CategoryModel>>(ReadSchemaFile("categories"))
					.ToDictionary(c => c.name, c => c.order);
			}

			return categories_;
		}

		public static List<TagModel> ReadAvailableTags()
		{
			return JsonConvert.DeserializeObject<List<TagModel>>(ReadSchemaFile("tags"));
		}

		public static SpecifierModel ReadSpecifierModel(string name)
		{
			return new SpecifierModel(JsonConvert.DeserializeObject<Dictionary<string, List<SpecifierParameterModel>>>(ReadSchemaFile(name)));
		}

		public static SpecifierSettings ReadSpecifierSettings(string name)
		{
			try
			{
				return JsonConvert.DeserializeObject<SpecifierSettings>(ReadSchemaFile("{0}.settings".format(name.ToLower())));
			}
			catch { }

			return new SpecifierSettings(new());
		}
	}

	public record struct CategoryModel(string name, int order);
	public record struct TagModel(string name, string type)
	{
		public override string ToString() => name.ToUpper();
	}

	public record struct SpecifierModel(Dictionary<string, List<SpecifierParameterModel>> collections);
	public record struct SpecifierParameterModel(string name, string category, string group, string type)
	{
		public bool IsEmpty => name.IsNullOrEmpty();


		public SpecifierParameterModel FixCategory(string defaultName)
			=> new SpecifierParameterModel(name, category.IsNullOrWhiteSpace() ? "Common" : category, group, type);

		public object DefaultValue
			=> type switch {
				"string" => (object)string.Empty,
				"bool" => (object)false,
				"integer" => (object)0,
				_ => (object)false,
			};

		public Type Type
			=> group.IsNullOrWhiteSpace()
			? type switch {
				"string" => typeof(string),
				"bool" => typeof(bool),
				"integer" => typeof(int),
				_ => typeof(bool),
			}
			: typeof(string);
	}

	public record struct SpecifierSettings(List<SpecifierOrder> order, SpecifierOrder defaultOrder = new())
	{
		public int GetParameterOrder(string parameterName)
		{
			foreach (var oi in order.Where(i => i.name == parameterName))
			{
				return oi.normalizedOrder;
			}

			if (defaultOrder.IsEmpty)
			{
				foreach (var di in order.Where(i => i.name == "*"))
				{ 
					return di.normalizedOrder;
				}

				defaultOrder = new(name: "*", order: 0);
			}

			return defaultOrder.normalizedOrder;
		}
	}

	public record struct SpecifierOrder(string name = "", int order = 0)
	{
		public bool IsEmpty => name.IsNullOrEmpty();

		public int normalizedOrder => order < 0 ? int.MaxValue + order : order;
	}
}
