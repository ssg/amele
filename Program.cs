﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace amele
{
    public static class Program
    {
        const string sourceCodeSearchPattern = "*.cs";
        static Regex classRegex = new Regex(@"\s+class\s+(?<className>[\w\d]+)");

        // [Index("IX_AdThemes_date_enabled", Order = 2)]
        // [Index("IX_AdThemes_date_enabled")]
        static Regex indexAttrRegex = new Regex(@"^
            \[
                Index\(
                    \""
                        (?<name>\w+)
                    \""
                (
                    \s*
                    \,
                    \s*
                    Order\s*
                    \=\s*
                    (?<order>\d+)
                )?
                \s*\)
            \]", RegexOptions.IgnorePatternWhitespace);
        static Regex propertyRegex = new Regex(@"^
            public
            \s+
            (?<type>[\w\?\<\>\.]+)
            \s+
            (?<name>\w+)
            \s*
            \{
                \s*
                get;
                \s*set;
                \s*
            \}", RegexOptions.IgnorePatternWhitespace);

        const string header = @"
// <auto-generated>
// Do not use this file directly but merge the code into your own modelbuilding code

public class HedeContext: DbContext
{
    public void OnModelBuilding(ModelBuilder builder)
    {
";

        const string footer = @"
    }
}";

        /// <summary>
        /// Convert EF6 Index attributes to EF Core declarative syntax.
        /// </summary>
        /// <param name="input">A folder where entity source code resides or entity source code file name.</param>
        /// <param name="output">A C# file that will be created from scratch to contain modelbuilding code.
        /// This parameter optional and if not given the output will be to the console.</param>
        static int Main(string input, string output)
        {
            string version = Assembly.GetEntryAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                .InformationalVersion;
            Console.WriteLine($"Amele v{version} - EF6 to EF Core Index attribute converter");
            if (input == null)
            {
                return Abort("--input isn't specified. Use --help for command-line help");
            }
            IEnumerable<string> files;
            if (Directory.Exists(input))
            {
                files = Directory.GetFiles(input, sourceCodeSearchPattern);
                if (!files.Any())
                {
                    return Abort("No files found in {0}", input);
                }
            }
            else
            {
                files = new string[] { input };
            }

            var entities = new List<Entity>();
            foreach (var fileName in files)
            {
                Console.Write("Processing {0}...", Path.GetFileName(fileName));
                if (tryGetEntity(fileName, out var entity))
                {
                    entities.Add(entity);
                    Console.WriteLine("DONE");
                }
            }

            if (entities.Count == 0)
            {
                Abort("No entities found - aborting");
                return 1;
            }

            Console.WriteLine("{0} entities collected", entities.Count);
            Console.Write("Writing model builder code to {0}...", output);

            var writer = output != null ? File.CreateText(output) : Console.Out;
            writer.WriteLine(header);

            foreach (var entity in entities)
            {
                foreach (var index in entity.Indexes)
                {
                    string indexFields = index.FieldNames.Count == 1
                        ? $"e.{index.FieldNames.Single()}"
                        : String.Format(CultureInfo.InvariantCulture, "new {{ {0} }}",
                            String.Join(", ", index.FieldNames.Select(f => $"e.{f}")));
                    writer.WriteLine($@"
        builder.Entity<{entity.Name}>()
            .HasIndex(e => {indexFields})
            .HasName(""{index.Name}"");");
                }
            }

            writer.WriteLine(footer);
            writer.Close();
            Console.WriteLine("SSG Operation complete.");
            return 0;
        }

        static int Abort(string message, params string[] args)
        {
            Console.Error.WriteLine(message, args);
            Environment.Exit(1);
            return 1;
        }

        static int Abort(string message)
        {
            Console.Error.WriteLine(message);
            Environment.Exit(1);
            return 1;
        }

        private static bool tryGetEntity(string fileName, out Entity entity)
        {
            entity = null;
            using var reader = new SourceReader(fileName);
            string className = getClassName(reader);
            if (className == null)
            {
                Console.WriteLine("Class name couldn't be found - skipping");
                return false;
            }
            Console.Write("{0}...", className);

            var properties = new List<Property>();
            while (tryGetNextIndexProperty(reader, out Property prop))
            {
                properties.Add(prop);
            }

            if (!properties.Any())
            {
                Console.WriteLine("No properties with [Index] attributes found - skipping");
                return false;
            }

            var indexes = properties.SelectMany(p => p.IndexAttrs)
                .GroupBy(a => a.IndexName)
                .Select(g => new Index
                {
                    Name = g.Key,
                    FieldNames = g.OrderBy(a => a.Order).Select(a => a.Property.Name).ToList(),
                })
                .ToList();
            entity = new Entity
            {
                Name = className,
                Indexes = indexes,
            };
            if (entity.Indexes.Count == 0)
            {
                Console.WriteLine("No indexes found");
                return false;
            }
            return true;
        }

        private static bool tryGetNextIndexProperty(SourceReader reader, out Property prop)
        {
            var attrs = new List<IndexAttr>();

            // accumulate all index properties together
            while (reader.TryReadLine(out string line))
            {
                var match = indexAttrRegex.Match(line);
                if (match.Success)
                {
                    attrs.Add(new IndexAttr
                    {
                        IndexName = match.Groups["name"].Value,
                        Order = match.Groups["order"].Success
                            ? int.Parse(match.Groups["order"].Value, CultureInfo.InvariantCulture)
                            : (int?)null,
                    });
                    continue; // read consecutive indexes until the property is hit
                }

                match = propertyRegex.Match(line);
                if (match.Success)
                {
                    if (attrs.Count == 0)
                    {
                        // skip non-attributed properties
                        continue;
                    }

                    prop = new Property()
                    {
                        IndexAttrs = attrs,
                        Name = match.Groups["name"].Value,
                    };
                    foreach (var attr in attrs)
                    {
                        attr.Property = prop;
                    }
                    return true;
                }
            }
            if (attrs.Count > 0)
            {
                Abort("Index found but no property!! coder error! KILL ALL HUMANS!");
            }
            prop = null;
            return false;
        }

        private static string getClassName(SourceReader reader)
        {
            while (reader.TryReadLine(out string line))
            {
                var match = classRegex.Match(line);
                if (match.Success)
                {
                    return match.Groups["className"].Value;
                }
            }
            return null;
        }
    }
}
