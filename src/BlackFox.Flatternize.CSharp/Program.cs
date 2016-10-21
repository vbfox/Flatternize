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

            var newUsings = root.Usings;

            if (newUsings.Count > 0)
            {
                var firstUsing = newUsings.First();
                var trivia = SyntaxFactory.TriviaList(SyntaxFactory.Comment("\r\n// Usings from " + doc.FilePath + "\r\n"))
                    .AddRange(firstUsing.GetLeadingTrivia());
                var newFirstUsing = firstUsing.WithLeadingTrivia(trivia);
                newUsings = newUsings.Replace(firstUsing, newFirstUsing);
            }

            var newMembers = root.Members;

            if (newMembers.Count > 0)
            {
                var firstMember = newMembers.First();
                var trivia = SyntaxFactory.TriviaList(SyntaxFactory.Comment("\r\n// Usings from " + doc.FilePath + "\r\n"))
                    .AddRange(firstMember.GetLeadingTrivia());
                var newFirstMember = firstMember.WithLeadingTrivia(trivia);
                newMembers = newMembers.Replace(firstMember, newFirstMember);
            }

            var usings = appendTo.Usings.AddRange(newUsings);
            var members = appendTo.Members.AddRange(newMembers);

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
            var path = @"G:\Code\_ext\Newtonsoft.Json\Src\Newtonsoft.Json.Net40.sln";
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
