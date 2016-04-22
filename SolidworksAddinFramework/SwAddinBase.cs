using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swpublished;
using SolidWorksTools;
using SolidWorksTools.File;

namespace SolidworksAddinFramework
{

    public interface ISolidworksAddin
    {
        ISldWorks SwApp { get; }
        IModeler Modeler { get; }
        MathUtility Math { get; }
    }


    [ComVisible(true)]
    public abstract class SwAddinBase : ISwAddin, ISolidworksAddin
    {
        private BitmapHandler _Bmp;
        private IDisposable _Disposable;

        protected ICommandManager CommandManager { get; private set; }

        #region ISolidworksAddin
        public ISldWorks SwApp { get; private set; }

        public MathUtility Math => (MathUtility) SwApp.GetMathUtility();

        public IModeler Modeler => (IModeler) SwApp.GetModeler();
        #endregion

        public static ISolidworksAddin Active { get; private set; }

        [ComRegisterFunction]
        public static void RegisterFunction(Type t)
        {
            try
            {
                var addinAttribute = t.GetCustomAttribute<SwAddinAttribute>(false);
                if (addinAttribute == null)
                {
                    throw new NullReferenceException($"Type {t.FullName} doesn't have {nameof(SwAddinAttribute)}.");
                }
                var hklm = Registry.LocalMachine;
                var hkcu = Registry.CurrentUser;

                var addinKey = CreateSubKey(hklm, "SOFTWARE\\SolidWorks\\Addins\\{" + t.GUID + "}");
                addinKey.SetValue(null, 0);

                addinKey.SetValue("Description", addinAttribute.Description);
                addinKey.SetValue("Title", addinAttribute.Title);

                var addinStartupKey = CreateSubKey(hkcu,"Software\\SolidWorks\\AddInsStartup\\{" + t.GUID + "}");
                addinStartupKey.SetValue(null, Convert.ToInt32(addinAttribute.LoadAtStartup), RegistryValueKind.DWord);
            }
            catch (Exception e)
            {
                Console.WriteLine($"There was a problem registering this dll: {e}");
            }
        }

        private static RegistryKey CreateSubKey(RegistryKey parentKey, string subKeyName)
        {
            Console.WriteLine($"Registering '{subKeyName}' to '{parentKey.Name}' ");
            return parentKey.CreateSubKey(subKeyName);
        }

        [ComUnregisterFunction]
        public static void UnregisterFunction(Type t)
        {
            try
            {
                var hklm = Registry.LocalMachine;
                var hkcu = Registry.CurrentUser;

                hklm.DeleteSubKey("SOFTWARE\\SolidWorks\\Addins\\{" + t.GUID + "}");
                hkcu.DeleteSubKey("Software\\SolidWorks\\AddInsStartup\\{" + t.GUID + "}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"There was a problem unregistering this dll: {e}");
            }
        }

        public bool ConnectToSW(object thisSw, int cookie)
        {
            SwApp = (ISldWorks)thisSw;

            Active = this;

            //Setup callbacks (i.e. when adding commands the callback name points to a method on this type)
            SwApp.SetAddinCallbackInfo2(0, this, cookie);

            CommandManager = SwApp.GetCommandManager(cookie);

            _Bmp = new BitmapHandler();
            var d0 = OpenGlRenderer.Setup((SldWorks) SwApp);
            var d1 = new CompositeDisposable(Setup());
            _Disposable = new CompositeDisposable(_Bmp, d0, d1);

            return true;
        }

        /// <summary>
        /// Get a bitmap resource string from bitmap name located relative
        /// to the assembly the current type is in.
        /// </summary>
        /// <example>
        /// <![CDATA[cmdGroup.LargeIconList = this.GetBitMap("MyProject.ToolbarLarge.bmp")]]>
        /// </example>
        /// <param name="name"></param>
        /// <param name="t">The type to use to look up the assembly the bitmap is relative to. If null
        /// uses the type of 'this'</param>
        /// <returns></returns>
        public string GetBitMap(string name, Type t = null)
        {
            var assembly = Assembly.GetAssembly(t ?? GetType());
            return _Bmp.CreateFileFromResourceBitmap(name, assembly);
        }

        /*
        public void AddTab(swDocumentTypes_e docType, string title)
        {
            
                var cmdTab = CommandManager.GetCommandTab(type, title);

                if (cmdTab != null & !getDataResult | ignorePrevious)//if tab exists, but we have ignored the registry info (or changed command group ID), re-create the tab.  Otherwise the ids won't matchup and the tab will be blank
                {
                    CommandManager.RemoveCommandTab(cmdTab);
                    cmdTab = null;
                }

                //if cmdTab is null, must be first load (possibly after reset), add the commands to the tabs
            if (cmdTab == null)
            {
            }
        }
        */

        protected abstract IEnumerable<IDisposable> Setup();

        public bool DisconnectFromSW()
        {
            _Disposable.Dispose();

            Marshal.ReleaseComObject(CommandManager);
            CommandManager = null;
            Marshal.ReleaseComObject(SwApp);
            SwApp = null;
            //The addin _must_ call GC.Collect() here in order to retrieve all managed code pointers 
            GC.Collect();
            GC.WaitForPendingFinalizers();

            GC.Collect();
            GC.WaitForPendingFinalizers();

            return true;
        }

        protected static bool CompareIDs(int[] storedIDs, int[] addinIDs)
        {
            var storedList = new HashSet<int>(storedIDs);
            var addinList = new HashSet<int>(addinIDs);
            return storedList.SetEquals(addinList);
        }
    }
}