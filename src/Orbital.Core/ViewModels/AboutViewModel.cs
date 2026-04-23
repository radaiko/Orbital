// src/Orbital.Core/ViewModels/AboutViewModel.cs
namespace Orbital.Core.ViewModels;

using System;

public sealed class AboutViewModel
{
    private readonly string repositoryUrl = "https://github.com/radaiko/Orbital";
    private readonly string copyright;

    public string Version { get; }
    public string RepositoryUrl => repositoryUrl;
    public string Copyright => copyright;

    public AboutViewModel(string version)
    {
        Version = version;
        copyright = $"© {DateTime.Now.Year} radaiko";
    }
}
