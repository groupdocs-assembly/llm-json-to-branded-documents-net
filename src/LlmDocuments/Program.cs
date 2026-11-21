using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GroupDocs.Assembly;

namespace LlmDocuments
{
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            ApplyLicense();

            if (args.Length == 0 || args[0] == "-h" || args[0] == "--help")
            {
                Usage();
                return args.Length == 0 ? 1 : 0;
            }

            try
            {
                switch (args[0].ToLowerInvariant())
                {
                    case "generate":
                        Pipeline.Generate(
                            Option(args, "--data") ?? "Customer.json",
                            Option(args, "--schema") ?? "Customer.schema.json");
                        return 0;

                    case "resources":
                        Pipeline.ExternalResources();
                        return 0;

                    case "typed":
                        Pipeline.TypedObjectsOverJson();
                        return 0;

                    case "extract":
                        return await Extract(args).ConfigureAwait(false);

                    default:
                        Console.Error.WriteLine($"Unknown command '{args[0]}'.");
                        Usage();
                        return 1;
                }
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("Error: " + exception.Message);
                return 1;
            }
        }

        private static async Task<int> Extract(string[] args)
        {
            string? sourcePath = args.Skip(1).FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));
            if (sourcePath == null || !File.Exists(sourcePath))
            {
                throw new FileNotFoundException("Pass a text file to extract from: extract <file.txt>", sourcePath ?? "(none)");
            }

            string schemaName = Option(args, "--schema") ?? "Customer.schema.json";
            string outputName = Option(args, "--out") ?? "llm-output.json";
            string model = Option(args, "--model") ?? ModelClient.DefaultModel;

            string schema = File.ReadAllText(RepositoryPaths.DataFile(schemaName));

            Console.WriteLine($"Asking {model} for JSON that satisfies {schemaName}...");
            string json = await ModelClient
                .ExtractAsync(File.ReadAllText(sourcePath), schema, ModelClient.ReadInstructions(), model, CancellationToken.None)
                .ConfigureAwait(false);

            string target = RepositoryPaths.DataFile(outputName);
            File.WriteAllText(target, json);

            Console.WriteLine("wrote  : " + target);
            Console.WriteLine($"Now run: generate --data {outputName} --schema {schemaName}");
            return 0;
        }

        private static string? Option(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        /// <summary>
        /// Optional: without a licence GroupDocs.Assembly runs in evaluation mode and the output
        /// carries evaluation marks. Point GROUPDOCS_ASSEMBLY_LICENSE at a .lic file to remove them.
        /// </summary>
        private static void ApplyLicense()
        {
            string? path = Environment.GetEnvironmentVariable("GROUPDOCS_ASSEMBLY_LICENSE")?.Trim();
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            if (File.Exists(path))
            {
                new License().SetLicense(path);
            }
            else
            {
                Console.Error.WriteLine("GROUPDOCS_ASSEMBLY_LICENSE points at a missing file: " + path);
            }
        }

        private static void Usage()
        {
            Console.WriteLine("Model output -> template -> branded document, with GroupDocs.Assembly for .NET");
            Console.WriteLine();
            Console.WriteLine("  generate  [--data Customer.json] [--schema Customer.schema.json]");
            Console.WriteLine("            assembles the statement as DOCX and PDF, checking the template against the schema");
            Console.WriteLine("  resources");
            Console.WriteLine("            shows what happens to an image URI that arrives in the data");
            Console.WriteLine("  typed");
            Console.WriteLine("            runs a template written for typed objects over the same data as JSON");
            Console.WriteLine("  extract   <file.txt> [--schema file] [--out file] [--model id]");
            Console.WriteLine("            asks a model for JSON that satisfies the schema (needs ANTHROPIC_API_KEY)");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  dotnet run --project src/LlmDocuments -- generate");
            Console.WriteLine("  dotnet run --project src/LlmDocuments -- resources");
        }
    }
}
