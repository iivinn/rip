using System;
using System.Collections.Generic;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Plugin.Rip
{
    public class Rip : IPlugin
    {
        private PluginInitContext _context;

        public void Init(PluginInitContext context)
        {
            _context = context;
        }

        public List<Result> Query(Query query)
        {
            var result = new Result
            {
                Title = "Paste video URL",
                SubTitle = "Paste video URL",
                Action = c =>
                {
                    _context.API.ShowMsg("Hello", "world!", "");
                    return true;
                }
            };

            return new List<Result>() { result };
        }
    }
}