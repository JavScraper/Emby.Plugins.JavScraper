using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.JavScraper.Execption
{
    public class PluginInitializeException : Exception
    {
        public PluginInitializeException(string message) : base(message)
        {
        }

        public PluginInitializeException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public PluginInitializeException()
        {
        }
    }
}
