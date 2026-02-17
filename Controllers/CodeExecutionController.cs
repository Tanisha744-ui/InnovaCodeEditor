using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using System.Text;
using InnovaCodeEditor.DTO;
using InnovaCodeEditor.Models;
using System.IO;

[ApiController]
[Route("api/[controller]")]
public class CodeExecutionController : ControllerBase
{
    [HttpPost("execute")]
    public IActionResult Execute([FromBody] CodeRequest request)
    {
        if (request.Files == null || request.Files.Count == 0)
            return BadRequest("No files provided");

        var syntaxTrees = new List<SyntaxTree>();
        foreach (var file in request.Files)
        {
            if (string.IsNullOrWhiteSpace(file.Code))
                continue;
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(file.Code, path: file.FileName));
        }
        if (syntaxTrees.Count == 0)
            return BadRequest("No valid code provided");

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly.Location)
        };

        var systemRuntimePath = Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "System.Runtime.dll");
        references.Add(MetadataReference.CreateFromFile(systemRuntimePath));

        var compilation = CSharpCompilation.Create(
            assemblyName: "InMemoryAssembly",
            syntaxTrees: syntaxTrees,
            references: references,
            options: new CSharpCompilationOptions(OutputKind.ConsoleApplication)
        );

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        // ❌ Compilation errors
        if (!result.Success)
        {
            var errors = result.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString());

            return Ok(new
            {
                output = string.Join("\n", errors)
            });
        }

        // ✅ Execute
        ms.Seek(0, SeekOrigin.Begin);
        var assembly = Assembly.Load(ms.ToArray());

        var entryPoint = assembly.EntryPoint;

        var output = new StringBuilder();
        var writer = new StringWriter(output);
        var originalOut = Console.Out;
        var originalIn = Console.In;

        try
        {
            Console.SetOut(writer);

            // Set input if provided
            if (!string.IsNullOrEmpty(request.Input))
            {
                var inputReader = new StringReader(request.Input);
                Console.SetIn(inputReader);
            }

            if (entryPoint.GetParameters().Length == 0)
                entryPoint.Invoke(null, null);
            else
                entryPoint.Invoke(null, new object[] { Array.Empty<string>() });
        }
        catch (Exception ex)
        {
            return Ok(new { output = ex.InnerException?.Message ?? ex.Message });
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetIn(originalIn);
        }

        return Ok(new { output = output.ToString() });
    }

}
