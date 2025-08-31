using System;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Serialization;
using System.Collections.Generic;

namespace Jellyfin.Plugin.ParentGuard
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        public static Plugin? Instance { get; private set; }
        public override string Name => "ParentGuard";

        // Note: Replace with a stable GUID before release
        public override Guid Id => new Guid("d5b86f5a-6a4c-4f7c-9f7c-8f52594b8b51");

        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
        }

        public void Save()
        {
            this.SaveConfiguration(Configuration);
        }

        public IEnumerable<PluginPageInfo> GetPages()
        {
            var ns = GetType().Namespace;
            return new[]
            {
                new PluginPageInfo
                {
                    Name = "parentguard",
                    EmbeddedResourcePath = ns + ".Web.parentguard.html"
                },
                new PluginPageInfo
                {
                    Name = "parentguard.js",
                    EmbeddedResourcePath = ns + ".Web.parentguard.js"
                },
                new PluginPageInfo
                {
                    Name = "parentguard.css",
                    EmbeddedResourcePath = ns + ".Web.parentguard.css"
                }
            };
        }
    }
}


