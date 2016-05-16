using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.MSBuild;

namespace Flatternize
{
    static class FluentRoslyn
    {
        public static CompilationUnitSyntax EmptyFile = SyntaxFactory.CompilationUnit();
    }

    internal static class EnumerableAsyncExtensions
    {
        public static async Task<TAccumulate> Aggregate<TSource, TAccumulate>(
            this IEnumerable<TSource> source,
            TAccumulate seed,
            Func<TAccumulate, TSource, Task<TAccumulate>> func)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (func == null) throw new ArgumentNullException(nameof(func));

            TAccumulate result = seed;
            foreach (TSource element in source)
            {
                result = await func(result, element);
            }
            return result;
        }

        public static async Task<TResult> Aggregate<TSource, TAccumulate, TResult>(
            this IEnumerable<TSource> source,
            TAccumulate seed,
            Func<TAccumulate, TSource, Task<TAccumulate>> func,
            Func<TAccumulate, Task<TResult>> resultSelector
            )
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (func == null) throw new ArgumentNullException(nameof(func));
            if (resultSelector == null) throw new ArgumentNullException(nameof(resultSelector));

            TAccumulate result = seed;
            foreach (TSource element in source)
            {
                result = await func(result, element);
            }
            return await resultSelector(result);
        }
    }

    class Program
    {
        private static MefHostServices MefHostServices
        {
            get
            {
                var assemblies = MefHostServices.DefaultAssemblies.Concat(new[] {Assembly.GetExecutingAssembly()});
                var hostServices = MefHostServices.Create(assemblies);
                return hostServices;
            }
        }

        private static async Task<CompilationUnitSyntax> AppendProject(CompilationUnitSyntax appendTo, Document doc)
        {
            var root = (CompilationUnitSyntax)await doc.GetSyntaxRootAsync();

            var usings = appendTo.Usings.AddRange(root.Usings);
            var members = appendTo.Members.AddRange(root.Members);

            return appendTo.WithUsings(usings).WithMembers(members);
        }

        static void Main(string[] args)
        {
            var doc = DoWork().Result;
            var text = doc.GetSyntaxRootAsync().Result.GetText().ToString();
            File.WriteAllText("Test.cs", text);
        }

        private static async Task<Document> DoWork()
        {
            var path = @"G:\Code\Nancy.Serialization.JsonNet\Nancy.Serialization.JsonNet.sln";
            var inputWorkspace = MSBuildWorkspace.Create();
            inputWorkspace.SkipUnrecognizedProjects = false;
            var solution = await inputWorkspace.OpenSolutionAsync(path);
            var inputProject = solution.Projects.First();

            var outputRoot = await inputProject.Documents.Aggregate(FluentRoslyn.EmptyFile, AppendProject);
            var outputWorkspace = new AdhocWorkspace();
            var outputProject = outputWorkspace.AddProject("MyProject", LanguageNames.CSharp);
            var doc = outputProject.AddDocument("MyDoc.cs", outputRoot);
            return doc;
        }
    }
}
