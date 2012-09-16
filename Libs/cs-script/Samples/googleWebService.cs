using System;
using System.Text;
using System.Xml;

//css_import GoogleSearchService;

namespace ConsoleApplication1
{
	class Program
	{
		static void Main(string[] args)
		{
			string googleKey = "yWje5FlQFHIDjiuRQzVyhVmvn9dSjsPp";

			
			if( args.Length == 0 || (args.Length == 1 && (args[0] == "?" || args[0] == "/?" || args[0] == "-?" || args[0].ToLower() == "help"))) 
			{
				Console.WriteLine("Usage: google.exe <query>\n");
				return;
			}

			string query = args[0];
			
			// Create a Google SOAP client proxy, generated by:
			// c:\> wsdl.exe http://api.google.com/GoogleSearch.wsdl
			GoogleSearchService googleSearch = new GoogleSearchService();

			GoogleSearchResult results = googleSearch.doGoogleSearch(googleKey, query, 0, 10, false, "", false, "", "latin1", "latin1");

			if (results.resultElements != null)
			{
				foreach (ResultElement result in results.resultElements)
				{
					Console.WriteLine();
					Console.WriteLine(result.title.Replace("<b>", "").Replace("</b>", ""));
					Console.WriteLine(result.URL);
					//Console.WriteLine(result.snippet);
					Console.WriteLine();
				}
			}
		}
	}
}
