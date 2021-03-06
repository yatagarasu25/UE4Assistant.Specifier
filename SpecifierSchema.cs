﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using SystemEx;



namespace UE4Assistant.Specifier
{
	public class SpecifierSchema
	{
		private static string ReadSchemaFile(string filename)
		{
			try
			{
				var stream = Application.GetResourceStream(
					new Uri(string.Format(@"/UE4Assistant.Specifier;component/Schema/{0}.json", filename), UriKind.Relative));
				return new StreamReader(stream.Stream).ReadToEnd();
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
			return JsonConvert.DeserializeObject<SpecifierModel>(ReadSchemaFile(name));
		}

		public static SpecifierSettings ReadSpecifierSettings(string name)
		{
			return JsonConvert.DeserializeObject<SpecifierSettings>(ReadSchemaFile("{0}.settings".format(name.ToLower())))
				?? new SpecifierSettings { order = new List<SpecifierOrder>() };
		}
	}

	public class CategoryModel
	{
		public string name;
		public int order;
	}

	public class TagModel
	{
		public string name;
	}

	public class SpecifierModel
	{
		public List<SpecifierParameterModel> parameters;
		public List<SpecifierParameterModel> meta;
	}

	public class SpecifierParameterModel
	{
		public string name;
		public string category;
		public string group;
		public string type;
	}

	public class SpecifierSettings
	{
		public List<SpecifierOrder> order;

		protected SpecifierOrder defaultOrder = null;

		public int GetParameterOrder(string parameterName)
		{
			var oi = order.Where(i => i.name == parameterName).FirstOrDefault();
			if (oi == null)
			{
				if (defaultOrder == null)
				{
					defaultOrder = order.Where(i => i.name == "*").FirstOrDefault();
					if (defaultOrder == null)
					{
						defaultOrder = new SpecifierOrder { name = "*", order = 0 };
					}
				}
				oi = defaultOrder;
			}
			int o = oi != null ? oi.order : 0;
			return o < 0 ? int.MaxValue + o : o;
		}
	}

	public class SpecifierOrder
	{
		public string name;
		public int order = 0;
	}
}
