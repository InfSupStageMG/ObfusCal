using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Components.Web;

[assembly: InternalsVisibleTo("ObfusCal.Tests")]

namespace ObfusCal.Api.Components.Pages;

public partial class ConsentCallback
{
    public const bool Prerender = false;

    public static InteractiveServerRenderMode RenderMode { get; } = new(prerender: Prerender);
}


