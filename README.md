# Generate Word and PDF documents from LLM output in C#

[![build](https://github.com/groupdocs-assembly/llm-json-to-branded-documents-net/actions/workflows/build.yml/badge.svg)](https://github.com/groupdocs-assembly/llm-json-to-branded-documents-net/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/GroupDocs.Assembly?label=GroupDocs.Assembly)](https://www.nuget.org/packages/GroupDocs.Assembly/)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%2010.0-512bd4)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

**To generate a Word or PDF document from LLM output in C#, have the model return JSON against a
JSON Schema, pass that schema to `JsonDataSource`, and let a Word template produce the document.**

A language model is good at reading a document and bad at producing one. So let it do the first
half: it returns **JSON**, and a Word template turns that JSON into the statement, the contract or
the report your company actually sends — fonts, logo, numbering and all.

The piece that makes this work in production is the **JSON Schema**. The same schema is the
contract you hand the model and the contract the template is checked against, so a field the model
left out renders as an empty line instead of failing the whole document. That, and the fact that a
URL arriving inside model output does not make your server fetch anything, are both new in
GroupDocs.Assembly for .NET **26.11**.

```
dotnet run --project src/LlmDocuments -- generate
```

![the generated statement](docs/output.png)

## Read this with

| Resource | What it adds |
|---|---|
| [Blog: Generate Word and PDF documents from LLM output in C#](https://blog.groupdocs.com/assembly/llm-json-to-branded-documents-csharp/) | why the schema is the contract, and what breaks without it |
| [Docs: Generate documents from model output](https://docs.groupdocs.com/assembly/net/generate-documents-from-llm-output/) | the reference walk-through, step by step |
| [This repository](https://github.com/groupdocs-assembly/llm-json-to-branded-documents-net) | the code that runs |

## Quick start

Requires the [.NET SDK 8.0 or later](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/groupdocs-assembly/llm-json-to-branded-documents-net.git
cd llm-json-to-branded-documents-net

# 1. JSON described by a schema -> DOCX and PDF
dotnet run --project src/LlmDocuments -- generate

# 2. what happens to an image URL that arrives in the data
dotnet run --project src/LlmDocuments -- resources

# 3. a template written for typed objects, over the same data as JSON
dotnet run --project src/LlmDocuments -- typed
```

The sample data in `data/Customer.json` is deliberately thin — an empty `Cards` array and a
`Loyalty` of `null`, the shape model output usually has:

```json
{ "Name": "Quayside Bistro", "Account": "00042", "Cards": [], "Loyalty": null }
```

### Letting a model produce the data

`generate` runs offline. To put a real model in front of it:

```bash
export ANTHROPIC_API_KEY=...        # setx on Windows
dotnet run --project src/LlmDocuments -- extract some-email.txt
dotnet run --project src/LlmDocuments -- generate --data llm-output.json
```

The prompt is [`prompts/extract-prompt.md`](prompts/extract-prompt.md) and the call is about fifty
lines in [`ModelClient.cs`](src/LlmDocuments/ModelClient.cs). GroupDocs.Assembly never sees the
model — it sees a JSON file and a schema — so any model or local runtime that can emit JSON fits.

## Why the schema, and not just the data

Without a schema the engine learns the shape of the data from the data. Model output has holes, so
the shape it learns is wrong in two ways that both end in a failed document:

* an **empty array** has no element type, so `<<[customer.Cards.First().Type]>>` cannot be resolved
  even when the branch around it never renders;
* an **account number** like `00042` looks like a number, so it arrives in the document as `42`.

With the schema, both are decided before the data is read:

```csharp
JsonDataSource dataSource = new JsonDataSource(
    "data/Customer.json",
    "data/Customer.schema.json");

new DocumentAssembler().AssembleDocument(
    "templates/Json schema.docx",
    "output/statement.docx",
    new DataSourceInfo(dataSource, "customer"));
```

The document above is the result: *No payment card on file*, an empty loyalty line, and the account
number still `00042`.

## Model output is untrusted data

A model can return a string that looks like an image reference. Since 26.11 a URI that comes from
**data** is not fetched; a URI the **template** itself carries still is. So the branding your
designer put in the template keeps working, and a value produced from somebody's e-mail does not
make your server issue requests.

```csharp
LoadSaveOptions options = new LoadSaveOptions();
options.WhitelistedResources.Add("images.example.com");          // hosts you trust
options.ExternalResourceHandler = new LocalImageHandler(path);   // see every decision
```

The handler can also hand over the bytes itself, which is how `resources` runs with no network at
all. Run it and watch the two documents it writes: one with the image left empty, one with the
image supplied locally.

## Layout

```
src/LlmDocuments/
  Program.cs          command line
  Pipeline.cs         the three GroupDocs.Assembly calls
  ModelClient.cs      optional: ask a model for the JSON
  RepositoryPaths.cs  finds templates/ and data/ wherever the binary runs from
prompts/extract-prompt.md
templates/            Json schema.docx, Inserting image.docx, Customer orders.docx
data/                 Customer.json, Customer.schema.json, Customers.json, no-photo.jpg
```

Every GroupDocs.Assembly call here mirrors an example of the product's examples repository
([GroupDocs.Assembly-for-.NET](https://github.com/groupdocs-assembly/GroupDocs.Assembly-for-.NET)),
so the code on the documentation pages, in the examples and in this repository stay the same.

## Notes

* **Licence.** Without one the library runs in evaluation mode and the output carries evaluation
  marks. Point `GROUPDOCS_ASSEMBLY_LICENSE` at a `.lic` file to remove them
  ([temporary licence](https://purchase.groupdocs.com/temporary-license)).
* **Frameworks.** The sample targets .NET 8. The package itself also builds for .NET Framework
  4.0 Client Profile, 4.6.2 and 4.8, .NET Standard 2.0, .NET 6 and .NET 10.

## Common questions

**How do I generate a Word document from LLM output in C#?**
Ask the model for JSON matching a JSON Schema, build `new JsonDataSource(jsonPath, schemaPath)` and
call `DocumentAssembler.AssembleDocument` with a `.docx` template.

**Why does `"00042"` become 42 in the document?**
Type inference from the data. Declare the field as `"type": "string"` in the schema.

**What if the model leaves an optional field out?**
With a schema the member is known and renders empty. Without one, see
[data-safe-templates-net](https://github.com/groupdocs-assembly/data-safe-templates-net).

**Can model output make the engine fetch a URL?**
Yes, if the data carries one and the template renders it. This sample shows how to whitelist hosts
or substitute a local image instead.

**Does it work with any model provider?**
Yes. The schema is the only contract; nothing in the template depends on the provider.

## Related samples

* [data-safe-templates-net](https://github.com/groupdocs-assembly/data-safe-templates-net) — the
  schema and the two options that decide what a missing member does
* [word-report-from-template-net](https://github.com/groupdocs-assembly/word-report-from-template-net) —
  the starting point: an object, a template, a report
* [GroupDocs.Assembly-for-.NET](https://github.com/groupdocs-assembly/GroupDocs.Assembly-for-.NET) —
  the full example set

## License

MIT, see [LICENSE](LICENSE). GroupDocs.Assembly itself is a commercial library with its own
[licence terms](https://purchase.groupdocs.com/policies/).
