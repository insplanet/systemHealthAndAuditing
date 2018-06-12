/****************************************************************************************
*	This code originates from the software development department at					*
*	swedish insurance and private loan broker Insplanet AB.								*
*	Full license available in license.txt												*
*	This text block may not be removed or altered.                                  	*
*	The list of contributors may be extended.                                           *
*																						*
*							Mikael Axblom, head of software development, Insplanet AB	*
*																						*
*	Contributors: Mikael Axblom															*
*****************************************************************************************/

using System;
using System.Reflection;

namespace SystemHealthExternalInterface
{
    [Serializable]
    public class ApplicationInfo
    {
        public ApplicationInfo()
        {
            ApplicationName = AppDomain.CurrentDomain.FriendlyName;
            ApplicationVersion = Assembly.GetExecutingAssembly().GetName().Version;
        }
        public ApplicationInfo(string applicatioName)
        {
            ApplicationName = applicatioName;
        }
        public string ApplicationName { get; set; }
        public string ApplicationLocation {get; set;}
        public Version ApplicationVersion { get; set; }
    }
}
