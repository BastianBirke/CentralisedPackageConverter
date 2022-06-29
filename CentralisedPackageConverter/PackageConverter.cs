﻿using System;
using System.IO;
using System.Xml.Linq;
using Microsoft.Build.Evaluation;

namespace CentralisedPackageConverter;

public class PackageConverter
{
	private IDictionary<string, string> allReferences = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	public void ProcessConversion( string solutionFolder )
	{
		var rootDir = new DirectoryInfo(solutionFolder);

		var projects = rootDir.GetFiles( "*.*", SearchOption.AllDirectories)
						      .Where(x => x.Extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase))
                              .OrderBy( x => x.Name );

		foreach( var project in projects )
        {
			ConvertProject( project );
        }

        if (allReferences.Any())
        {
            WriteDirectoryPackagesConfig(solutionFolder);
        }
        else
            Console.WriteLine("No references found...");
    }

    /// <summary>
    /// Write the packages.config file.
    /// TODO: Would be good to read the existing file and merge if appropriate.
    /// </summary>
    /// <param name="solutionFolder"></param>
	private void WriteDirectoryPackagesConfig(string solutionFolder)
    {
        var lines = new List<string>();

        lines.Add("<Project>");
        lines.Add("  <PropertyGroup>");
        lines.Add("    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>");
        lines.Add("  </PropertyGroup>");
        lines.Add("  <ItemGroup>");

        foreach (var kvp in allReferences.OrderBy(x => x.Key))
        {
            lines.Add($"    <PackageVersion Include=\"{kvp.Key}\" Version=\"{kvp.Value}\" />");
        }

        lines.Add("  </ItemGroup>");
        lines.Add("</Project>");

        Console.WriteLine($"Writing {allReferences.Count} refs to Directory.Packages.props to {solutionFolder}...");

        var packageConfig = Path.Combine(solutionFolder, "Directory.Packages.props");

        File.WriteAllLines(packageConfig, lines);
    }

    private string? GetAttributeValue( XElement elem, string name, bool remove )
    {
		var attr = elem.Attributes(name);

		if( attr != null )
        {
			var value = attr.Select(x => x.Value).FirstOrDefault();
            if( remove )
				attr.Remove();
			return value;
        }

        return null;
    }

	private void ConvertProject( FileInfo csprojFile )
    {
        Console.WriteLine($"Processing references for {csprojFile.FullName}...");

        var xml = XDocument.Load( csprojFile.FullName);

		var refs = xml.Descendants("PackageReference");

		bool rewriteCsproj = false;

		foreach( var reference in refs )
        {
			var package = GetAttributeValue( reference, "Include", false );
            var version = GetAttributeValue( reference, "Version", true ); 

			if( string.IsNullOrEmpty( package ))
				continue;

			if (!string.IsNullOrEmpty(version))
			{
				rewriteCsproj = true;

                if (allReferences.TryGetValue(package, out var existingVer))
				{
					// Existing reference for this package of same or greater version, so skip
					if (version.CompareTo(existingVer) >= 0)
						continue;
				}

                Console.WriteLine($" Found new reference: {package} {version}");
				allReferences[package] = version;
            }
        }

		if( rewriteCsproj )
			xml.Save(csprojFile.FullName);
    }
}

