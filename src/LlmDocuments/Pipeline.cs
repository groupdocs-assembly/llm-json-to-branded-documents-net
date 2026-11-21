using System;
using System.IO;
using GroupDocs.Assembly;
using GroupDocs.Assembly.Data;

namespace LlmDocuments
{
    /// <summary>
    /// The three GroupDocs.Assembly calls this sample is about. Each one mirrors an example of
    /// the official examples repository, so the code on the documentation pages and the code here
    /// stay the same:
    /// GroupDocs.Assembly-for-.NET/Examples/GroupDocs.Assembly.Examples.CSharp/AdvancedUsage.
    /// </summary>
    public static class Pipeline
    {
        /// <summary>
        /// Assembles the document from model output described by a JSON Schema, and writes both
        /// the DOCX and a PDF of it.
        ///
        /// The schema is what makes incomplete model output safe: members declared in it can be
        /// used by the template even when the data has them empty, null or missing, and their
        /// types come from the schema rather than from the first value that happens to be there.
        /// Supported since 26.11.
        /// </summary>
        public static void Generate(string dataFileName, string schemaFileName)
        {
            string dataPath = RepositoryPaths.DataFile(dataFileName);
            string schemaPath = RepositoryPaths.DataFile(schemaFileName);

            Console.WriteLine($"data   : {dataPath}");
            Console.WriteLine($"schema : {schemaPath}");

            JsonDataSource dataSource = new JsonDataSource(dataPath, schemaPath);

            DocumentAssembler assembler = new DocumentAssembler();
            assembler.AssembleDocument(
                RepositoryPaths.Template("Json schema.docx"),
                RepositoryPaths.OutputFile("statement.docx"),
                new DataSourceInfo(dataSource, "customer"));

            // The same template and the same data, saved as PDF instead.
            JsonDataSource pdfDataSource = new JsonDataSource(dataPath, schemaPath);

            assembler.AssembleDocument(
                RepositoryPaths.Template("Json schema.docx"),
                RepositoryPaths.OutputFile("statement.pdf"),
                new LoadSaveOptions(FileFormat.Pdf),
                new DataSourceInfo(pdfDataSource, "customer"));

            Console.WriteLine("wrote  : " + RepositoryPaths.OutputFile("statement.docx"));
            Console.WriteLine("wrote  : " + RepositoryPaths.OutputFile("statement.pdf"));
        }

        /// <summary>
        /// Shows what happens to a URI that arrives in the data rather than in the template.
        ///
        /// Since 26.11 such a reference is not fetched by default, which is the behaviour you want
        /// when the data was produced by a model from a document somebody else supplied. Hosts you
        /// trust go into <c>WhitelistedResources</c>, and an <c>ExternalResourceHandler</c> sees
        /// every decision - and can supply the bytes itself instead of letting the server request
        /// anything at all.
        /// </summary>
        public static void ExternalResources()
        {
            // The image URI comes from data, so it is skipped by default and the image is left empty.
            const string photoUri = "https://images.example.com/photos/no-photo.jpg";

            DocumentAssembler assembler = new DocumentAssembler();
            assembler.AssembleDocument(
                RepositoryPaths.Template("Inserting image.docx"),
                RepositoryPaths.OutputFile("logo-skipped.docx"),
                new DataSourceInfo(photoUri, "expression"));

            // Allow a trusted host and see every decision. The handler can also provide the content
            // itself, here from a local file, so the sample needs no network access.
            LoadSaveOptions options = new LoadSaveOptions();
            options.WhitelistedResources.Add("images.example.com");
            options.ExternalResourceHandler = new LocalImageHandler(RepositoryPaths.DataFile("no-photo.jpg"));

            assembler.AssembleDocument(
                RepositoryPaths.Template("Inserting image.docx"),
                RepositoryPaths.OutputFile("logo-provided.docx"),
                options,
                new DataSourceInfo(photoUri, "expression"));

            Console.WriteLine("wrote  : " + RepositoryPaths.OutputFile("logo-skipped.docx") + "  (image left empty)");
            Console.WriteLine("wrote  : " + RepositoryPaths.OutputFile("logo-provided.docx") + "  (bytes supplied locally)");
        }

        /// <summary>
        /// A template written for typed objects, run over the same data as JSON.
        ///
        /// Since 26.11 the JSON path behaves like the object path: members of dates and numbers
        /// that may be missing, Booleans in conditions and predicates, Json.NET <c>$id</c>/<c>$ref</c>
        /// references, and grouping over nested objects that compare by value.
        /// </summary>
        public static void TypedObjectsOverJson()
        {
            DocumentAssembler assembler = new DocumentAssembler();
            assembler.AssembleDocument(
                RepositoryPaths.Template("Customer orders.docx"),
                RepositoryPaths.OutputFile("orders.docx"),
                new DataSourceInfo(new JsonDataSource(RepositoryPaths.DataFile("Customers.json")), "customers"));

            Console.WriteLine("wrote  : " + RepositoryPaths.OutputFile("orders.docx"));
        }

        /// <summary>Logs every external resource decision and provides the trusted ones from disk.</summary>
        public sealed class LocalImageHandler : ExternalResourceHandler
        {
            private readonly string mImagePath;

            public LocalImageHandler(string imagePath)
            {
                mImagePath = imagePath;
            }

            public override void OnLoading(ExternalResourceLoadingArgs args)
            {
                Console.WriteLine($"  {args.Origin} resource {args.Uri}: {(args.Skipped ? "skipped" : "allowed")}");

                // Provide the content instead of requesting it.
                if (!args.Skipped && args.Uri.StartsWith("https://images.example.com/", StringComparison.OrdinalIgnoreCase))
                {
                    args.Data = File.ReadAllBytes(mImagePath);
                }
            }
        }
    }
}
