using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using HealthAndAuditShared;

namespace ControlCentre
{
    internal static class HelperMethods
    {
        public static DocumentDBRuleStorage GetRuleStorage()
        {
            return new DocumentDBRuleStorage(ConfigurationManager.AppSettings["DocDBEndPointUrl"], ConfigurationManager.AppSettings["AuthorizationKey"], ConfigurationManager.AppSettings["RuleDatabaseId"], ConfigurationManager.AppSettings["RuleCollectionId"]);
        }


        /// <summary>
        /// Formats the exception (and inner exceptions) to an easy to read format with the information we want.
        /// * Fullname 
        /// * Message
        /// * Target
        /// * Formatted StackTrace 
        /// </summary>
        /// <param name="ex">The exception to format.</param>
        /// <returns>A multi line string with the information we want from the exception.</returns>
        public static string FormatException(Exception ex)
        {
            if (ex == null)
            {
                return "No exception.";
            }
            var formated = new StringBuilder();
            formated.Append("<<< EXCEPTION >>>");
            formated.Append("<br>Fullname: " + ex.GetType().FullName);
            formated.Append("<br>Message: " + ex.Message);
            formated.Append("<br>Target: " + ex.TargetSite);
            formated.Append("<br>StackTrace: " + FormatStackTrace(ex.StackTrace));

            if (ex.InnerException != null)
            {
                formated.Append("<br>     InnerException: " + ex.InnerException.GetType().FullName);
                formated.Append("<br>     Message: " + ex.InnerException.Message);
                formated.Append("<br>     Target: " + ex.InnerException.TargetSite);
                formated.Append("<br>     StackTrace: " + FormatStackTrace(ex.InnerException.StackTrace));
            }
            return formated.ToString();
        }
        /// <summary>
        /// Formats the stack trace with line breaks and indents.
        /// </summary>
        /// <param name="stacktrace">The stacktrace.</param>
        /// <returns>Formated stacktrace</returns>
        private static string FormatStackTrace(string stacktrace)
        {
            return stacktrace?.Replace(" at ", "<br><br>at ").Replace(" in ", "<br>in ") ?? string.Empty;
        }


        /// <summary>
        /// Gets a dump of object information.
        /// Type of the object.
        /// Value of the object.
        /// Will dig through <see cref="InspectedClassAttribute"/> marked classes. And report all of their <see cref="InspectorReportAttribute"/> marked properties.
        /// </summary>
        /// <param name="objects">Dictionary of objects with their names for Keys.</param>
        /// <returns>Formatted string with object information</returns>
        public static string GetObjectDump(Dictionary<string, object> objects)
        {
            if (objects == null)
            {
                return string.Empty;
            }
            var ret = "<<< Operation Parameters >>>";
            foreach (var objInfo in objects)
            {
                ret += GetObjectDump("<br>", "     ", objInfo);
            }
            return ret;
        }

        /// <summary>
        /// Gets the object dump.
        /// </summary>
        /// <param name="newLine">The new line replacment string.</param>
        /// <param name="indent">The indent replacment string.</param>
        /// <param name="objInfo">The object information, name and object itself.</param>
        /// <returns></returns>
        private static string GetObjectDump(string newLine, string indent, KeyValuePair<string, object> objInfo)
        {
            var dump = newLine + indent;
            if (objInfo.Value == null)
            {
                dump += $"{objInfo.Key} is null.";
            }
            else
            {
                var objectType = objInfo.Value.GetType();
                dump += $"{objInfo.Key} of type {objectType.FullName} ";
                if (objInfo.Value is System.Collections.IEnumerable && objectType != typeof(string))
                {
                    var enumb = objInfo.Value as System.Collections.IEnumerable;
                    int i = 0;
                    foreach (object obj in enumb)
                    {
                        dump += GetObjectDump(newLine, indent + indent, new KeyValuePair<string, object>($"[{i++}]", obj));
                    }
                }
                else
                {
                    dump += $" Value: {objInfo.Value}";
                }
            }
            return dump;
        }
    }
}