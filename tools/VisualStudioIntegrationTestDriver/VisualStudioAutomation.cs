using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace SpireValidation
{
    public static class VisualStudioAutomation
    {
        public static object TryGetDte(int processId)
        {
            IBindCtx context;
            Marshal.ThrowExceptionForHR(CreateBindCtx(0, out context));
            IRunningObjectTable table = null;
            IEnumMoniker iterator = null;
            try
            {
                context.GetRunningObjectTable(out table);
                table.EnumRunning(out iterator);
                var items = new IMoniker[1];
                while (iterator.Next(1, items, IntPtr.Zero) == 0)
                {
                    try
                    {
                        string name;
                        items[0].GetDisplayName(context, null, out name);
                        if (name.TrimStart('!').StartsWith("VisualStudio.DTE.", StringComparison.Ordinal) &&
                            name.EndsWith(":" + processId, StringComparison.Ordinal))
                        {
                            object dte;
                            table.GetObject(items[0], out dte);
                            return dte;
                        }
                    }
                    finally { Marshal.ReleaseComObject(items[0]); }
                }
                return null;
            }
            finally
            {
                if (iterator != null) Marshal.ReleaseComObject(iterator);
                if (table != null) Marshal.ReleaseComObject(table);
                Marshal.ReleaseComObject(context);
            }
        }

        [DllImport("ole32.dll")]
        private static extern int CreateBindCtx(uint reserved, out IBindCtx context);
    }
}
