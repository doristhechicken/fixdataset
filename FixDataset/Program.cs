using Microsoft.CSharp;
using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Data.Design;

namespace FixDataset
{

	class Program
	{

		const string usage = "\r\nUsage: FixDataset -xsd \\path\\to\\xsd_file -paramfile <inputfile> | [-adapter <table_adapter_name> -method <method_name> -sql <sql> | -query <querytype> | [-paramname <paramname> -paramtype <paramtype>]] -regen namespace\r\n" +
			"-xsd: full path to the .xsd file to alter (required).\r\n" +
			"-paramfile: (optional) a file with one line for each operation using following parameters, or ':' for comment lines.\r\n" +
			"-adapter: name of tableadapter to modify (required)\r\n" +
			"-method: name of method to modify (required). Default Methods are called Select,Delete,Update,Insert.\r\n" +
			"-sql: set the SQL for the method. (VS rewrites WHERE clauses into long winded drivel).\r\n" +
			"-query: set the querytype to NonQuery/Scalar. Common use: reset an Insert method to Scalar (VS changes to NonQuery).\r\n" +
			"-paramname: Specify the Method parameter name to alter:\r\n" +
			"-paramtype: Change a Method DbType to this. String default size is 1024, rest size should be 1. (String/Boolean/Int32/DateTime allowed).\r\n" +
			"-paramsize: if paramtype is String, specify size. Default is 1024. Ignored for other types.\r\n" +
			"-regen namespace: (commandline only - not allowed in paramfile) regenerate the .designer.cs file from an altered .xsd file.\r\n\r\n" +
			"Can specify only one operation per commandline. For multiple operations, use a parameter file with one set of parameters per line.";

		static void Main()
		{

			ArgItemCollection argitems = new ArgItemCollection();

			argitems.Add(new ArgItem("xsd", null, ArgTypeEnum.Path, false));
			argitems.Add(new ArgItem("paramfile", null, ArgTypeEnum.Path, false));
			argitems.Add(new ArgItem("adapter", null, ArgTypeEnum.Text, false));
			argitems.Add(new ArgItem("method", null, ArgTypeEnum.Text, false));
			argitems.Add(new ArgItem("sql", null, ArgTypeEnum.Text, false));
			argitems.Add(new ArgItem("query", null, ArgTypeEnum.Text, false));
			argitems.Add(new ArgItem("paramname", null, ArgTypeEnum.Text, false));
			argitems.Add(new ArgItem("paramtype", null, ArgTypeEnum.Text, false));
			argitems.Add(new ArgItem("paramsize", null, ArgTypeEnum.Number, false));
			argitems.Add(new ArgItem("regen", null, ArgTypeEnum.Text, false));
			StreamReader sr = null;

			try
			{
				string line = Environment.CommandLine;
				argitems.LoadArgs(line, true);
				// load up the xsd file contents into a string.
				if (!argitems.GetArgItem("xsd").Specified) throw new Exception("Requiring parameter missing: -xsd");
				string xsdfile = argitems["xsd"];
				XDocument xsd = XDocument.Load(xsdfile);

				// either process a file of operations or just process the command line
				if (argitems.GetArgItem("paramfile").Specified)
				{
					// check no other parameters specified
					if (argitems.GetArgItem("adapter").Specified || argitems.GetArgItem("method").Specified || argitems.GetArgItem("sql").Specified || argitems.GetArgItem("query").Specified || argitems.GetArgItem("paramname").Specified || argitems.GetArgItem("paramtype").Specified) throw new Exception("Invalid commandline. If -paramfile specified all other parameters must be in the file.");
					// read each line from the file and load args
					try
					{
						sr = new StreamReader(argitems["paramfile"]);
						int lineno = 0;
						while (!sr.EndOfStream)
						{
							line = sr.ReadLine();
							if ((line.Length > 0) && (line[0] != ':')) // blank line or : (comment) skipped
							{
								argitems.Reset();
								argitems.LoadArgs(line, false);
								if (argitems.GetArgItem("xsd").Specified || argitems.GetArgItem("paramfile").Specified || argitems.GetArgItem("regen").Specified)
									throw GetException("-xsd, -paramfile, -regen are invalid in a paramfile. ", lineno, line);
								Process(xsd, argitems, lineno, line);
							}
							lineno++;
						}
						sr.Close();
					}
					finally
					{
						if (sr != null) sr.Close();
					}
				}
				else Process(xsd, argitems, -1, line);

				// write out the modified xsd file
				// create backup
				//xsdfile

				string tempfile = Path.ChangeExtension(xsdfile, ".temp.xsd");
				string bakfile = Path.ChangeExtension(xsdfile, ".xsd.bak");

				xsd.Save(tempfile);
				File.Delete(bakfile);
				File.Move(xsdfile, bakfile);
				File.Move(tempfile, xsdfile);
				Console.WriteLine("FixXSD: .xsd File processed successfully.");
				if (argitems.GetArgItem("regen").Specified)
				{
					string nspace = argitems["regen"];
					if (string.IsNullOrEmpty(nspace)) nspace = Path.GetFileNameWithoutExtension(xsdfile);

					sr = null;
					string xsdfilecontent = null;
					try
					{
						sr = new StreamReader(xsdfile);
						xsdfilecontent = sr.ReadToEnd();
						sr.Close();
					}
					finally
					{
						if (sr != null) sr.Close();
					}
					StreamWriter filewriter = null;
					try
					{
						var codeCompileUnit = new CodeCompileUnit();
						var codeNamespace = new CodeNamespace(argitems["n"]);
						Dictionary<string, string> providerOptions = new Dictionary<string, string>();
						providerOptions.Add("CompilerVersion", "v4");
						var codeDomProvider = CodeDomProvider.CreateProvider("CSharp", providerOptions);

						TypedDataSetGenerator.Generate(xsdfilecontent, codeCompileUnit, codeNamespace, codeDomProvider, TypedDataSetGenerator.GenerateOption.HierarchicalUpdate | TypedDataSetGenerator.GenerateOption.LinqOverTypedDatasets);
						string outputfile = Path.ChangeExtension(xsdfile, ".designer.cs");

						bakfile = outputfile + ".bak";

						File.Delete(bakfile);
						File.Move(outputfile, bakfile);
						filewriter = new StreamWriter(outputfile, false);
						var generatorOptions = new CodeGeneratorOptions();

						var cscodeprovider = new CSharpCodeProvider();
						cscodeprovider.GenerateCodeFromNamespace(codeNamespace, filewriter, generatorOptions);
						cscodeprovider.GenerateCodeFromCompileUnit(codeCompileUnit, filewriter, generatorOptions);
						Console.Write("FixXSD: TypedDataSet (.designer.cs) regenerated sucessfully.");
					}
					finally
					{
						if (filewriter != null) filewriter.Close();
					}

				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
				Console.WriteLine(usage);
#if DEBUG
				Console.ReadKey();
#endif
				return;
			}

		}

		/*
		   "-adapter: name of tableadapter to modify (required)\r\n" +
			"-method: name of method to modify (required)\r\n" +
			"-sql: set the SQL for the method. VS rewrites WHERE clauses into long winded drivel.\r\n" +
			"-query: set the querytype to NonQuery/Scalar. Common use: set an Insert method to Scalar (VS will change to NonQuery).\r\n" +
			"-paramname: Specify a Method parameter name.\r\n" +
			"-paramtype: Change a Method DBType to this. String size set to 1024, rest size set to 1. (String/Boolean/Int32/DateTime allowed).\r\n\r\n" +
		 */
		static void Process(XDocument xsd, ArgItemCollection args, int line, string linetext)
		{

			if (!args.GetArgItem("adapter").Specified) throw GetException("Required parameter missing: -adapter", line, linetext);
			if (!args.GetArgItem("method").Specified) throw GetException("Required parameter missing: -method", line, linetext);
			string adapter = args["adapter"];
			string method = args["method"];
			string query = args["query"]?.ToLower();
			string paramname = args["paramname"];
			if (!string.IsNullOrEmpty(paramname) && paramname[0] != '@') paramname = '@' + paramname;
			string paramtype = args["paramtype"]?.ToLower();

			var ns = xsd.Root.Name.Namespace;
			var appinfo_xElement = xsd.Element(ns + "schema").Element(ns + "annotation").Element(ns + "appinfo");
			ns = appinfo_xElement.Attribute("source").Value;
			var ta_xElements = appinfo_xElement.Descendants(ns + "TableAdapter").Where(item => string.Compare(item.Attribute("DataAccessorName").Value, adapter, true) == 0);
			if (ta_xElements.Count() != 1) throw GetException("Found " + ta_xElements.Count().ToString() + " TableAdapters called " + adapter, line, linetext);

			var method_xElements = ta_xElements.First().Element(ns + "Sources").Elements(ns + "DbSource").Where(item => string.Compare(item.Attribute("Name")?.Value, method, true) == 0 || string.Compare(item.Attribute("GetMethodName")?.Value, method, true) == 0);
			if (method_xElements.Count() != 1) throw GetException("Found " + method_xElements.Count().ToString() + " Methods called " + adapter + "." + method, line, linetext);

			if (args.GetArgItem("sql").Specified)
			{
				method_xElements.Descendants(ns + "CommandText").First().Value = args["sql"];
				Console.WriteLine("FixXSD: SQL replaced for " + adapter + "." + method);
			}
			if (args.GetArgItem("query").Specified) // set querytype
			{
				var querytype_attr = method_xElements.First().Attribute("QueryType");
				if (query == "scalar") querytype_attr.Value = "Scalar";
				else if (query == "nonquery") querytype_attr.Value = "NonQuery";
				else throw GetException("Invalid querytype: " + query, line, linetext);
				Console.WriteLine("FixXSD: QueryType updated for " + adapter + "." + method);
			}
			if (args.GetArgItem("paramname").Specified)
			{
				if (!args.GetArgItem("paramtype").Specified) throw GetException("Required parameter missing: -paramtype", line, linetext);

				var param_xElements = method_xElements.First().Descendants(ns + "Parameter").Where(item => string.Compare(item.Attribute("ParameterName")?.Value, paramname, true) == 0);
				if (param_xElements.Count() != 1) throw GetException("Found " + param_xElements.Count().ToString() + " Parameters called " + adapter + "." + method + "." + paramname, line, linetext);

				string dbtype, providertype, size = "1";
				switch (paramtype)
				{
					case "boolean":
						dbtype = "Boolean"; providertype = "Bit";
						break;
					case "string":
						dbtype = "String"; providertype = "NVarChar";
						if (args.GetArgItem("paramsize").Specified) size = args["paramsize"];
						else size = "1024";
						break;
					case "int32":
						dbtype = "Int32"; providertype = "Int";
						break;
					case "datetime":
						dbtype = providertype = "DateTime";
						break;
					default:
						throw GetException("Invalid ParamType: " + args["paramtype"], line, linetext);
				}

				param_xElements.First().Attribute("DbType").Value = dbtype;
				if (param_xElements.First().Attribute("ProviderType") != null) param_xElements.First().Attribute("ProviderType").Value = providertype;
				param_xElements.First().Attribute("Size").Value = size;

				Console.WriteLine("FixXSD: Parameter type fixed for " + adapter + "." + method + "." + paramname);
			}

		}

		/// <summary>
		/// Exception text has "Line x: " added in front where line != -1
		/// </summary>
		/// <param name="ex"></param>
		/// <param name="line"></param>
		/// <returns></returns>
		static Exception GetException(string ex, int line, string linetext)
		{
			return new Exception((line == -1 ? "" : "Line ") + (line == -1 ? "" : line.ToString() + ": ") + ex + " \"" + linetext + "\"");
		}

	}
}

public enum ArgTypeEnum { Text = 0, Number = 1, Path = 2, ParamOnly = 3 }

public class ArgItem
{
	public string ArgParam { get; protected set; }
	public string ArgParam2 { get; protected set; } // alternative item
	public ArgTypeEnum ArgType { get; protected set; }
	public bool Required { get; protected set; }
	public string Value { get; set; }
	public bool Specified { get; set; }
	/// <summary>
	/// Create Argument Item.
	/// </summary>
	/// <param name="argParam">Parameter name</param>
	/// <param name="argParam2">Alternate name (eg shorter)</param>
	/// <param name="argType">Type</param>
	/// <param name="required">True if required</param>
	public ArgItem(string argParam, string argParam2, ArgTypeEnum argType, bool required)
	{
		ArgParam = argParam.ToLower();
		ArgParam2 = argParam2?.ToLower();
		ArgType = argType;
		Value = null;
		Required = required;
	}
}

public class ArgItemCollection : System.Collections.ReadOnlyCollectionBase
{
	public void Add(ArgItem item) { InnerList.Add(item); }
	public ArgItem this[int index]
	{
		get
		{
			if ((index < 0) || (index >= InnerList.Count)) throw new IndexOutOfRangeException();
			return (ArgItem)InnerList[index];
		}
	}
	public ArgItem GetArgItem(string param)
	{
		param = param.ToLower();
		for (int i = 0; i < InnerList.Count; i++) if (((ArgItem)InnerList[i]).ArgParam == param) return ((ArgItem)InnerList[i]);
		return null;
	}
	public string this[string param]
	{
		get
		{
			ArgItem item = GetArgItem(param);
			if (item == null) return null;
			else return item.Value;
		}
		set
		{
			ArgItem item = GetArgItem(param);
			if (item != null) item.Value = value;
		}
	}
	/// <summary>
	/// Reset all Value / Specified fields to empty string and false.
	/// </summary>
	public void Reset()
	{
		for (int i = 0; i < InnerList.Count; i++)
		{
			ArgItem item = ((ArgItem)InnerList[i]);
			item.Value = string.Empty;
			item.Specified = false;
		}
	}

	/// <summary>
	/// Usage: MDWCLUtil.LoadArgs(argitems,Environment.CommandLine); Deals with " and paths correctly.
	/// </summary>
	/// <param name="argitems"></param>
	/// <param name="commandline"></param>
	/// <param name="skipcommandname">false if command line is from a file etc and first argument is not the filename</param>
	public void LoadArgs(string commandline, bool skipcommandname = true)
	{
		// parse commandline
		// Rules: " ignored unless at start or end of an arg ie space before and after
		// "stuff with spaces" => stuff with spaces
		// otherwise args are separated by a space
		// no escapes allowed \" cos this mucks up directories
		List<string> args = new List<string>();
		int i = 0;
		bool isquoted = false;
		int clen = commandline.Length;
		StringBuilder sb = new StringBuilder();
		while (i < clen)
		{
			char c = commandline[i++];
			if ((c == '\r') || (c == '\n')) throw new Exception("Newline in command line not allowed: " + commandline);
			if ((c == '"') && isquoted)
			{
				if ((i < clen) && (!char.IsWhiteSpace(commandline[i]))) throw new Exception("Quote in middle of string not allowed: " + commandline);
				i++; // skip the ' '
				args.Add(sb.ToString());
				sb.Clear();
				isquoted = false;
			}
			else if ((c == '"') && (sb.Length == 0) && !isquoted)
			{
				isquoted = true;
			}
			else if ((c == ' ') && !isquoted)
			{
				if (sb.Length > 0) args.Add(sb.ToString());
				sb.Clear();
			}
			else sb.Append(c);
		}
		if (isquoted) throw new Exception("Unmatched quote: " + commandline);
		if (sb.Length > 0) args.Add(sb.ToString());

		string param = null;
		bool found;
		for (i = 0; i < args.Count; i++)
		{
			if ((i == 0) && skipcommandname) continue;
			string arg = args[i].Trim();
			if (arg.StartsWith("-"))
			{

				if (arg.Length < 2) throw new Exception("Invalid parameter.");
				if (param != null) throw new Exception("Invalid parameter - argument value missing.");
				arg = arg.Substring(1).ToLower();
				found = false;
				foreach (ArgItem argitem in this)
				{
					if ((arg == argitem.ArgParam) || (!string.IsNullOrEmpty(argitem.ArgParam2) && (arg == argitem.ArgParam2)))
					{
						if (argitem.Specified) throw new Exception("Duplicate parameter: \"" + arg + "\"");
						argitem.Specified = true;
						if (!(argitem.ArgType == ArgTypeEnum.ParamOnly)) param = argitem.ArgParam;
						found = true;
						break;
					}
				}
				if (!found) throw new Exception("Unknown argument \"" + arg + "\"");
			}
			else
			{
				if (param == null) throw new Exception("Parameter expected, have paths/keys been quote-enclosed? \"" + arg + "\"");
				this[param] = arg;
				param = null;
			}
		}
		foreach (ArgItem argitem in this) if ((argitem.Required) && !argitem.Specified) throw new Exception("Required parameter missing: -" + argitem.ArgParam);
	}

}

