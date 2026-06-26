using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace BothPerks.Bootstrap
{
    public sealed class SubModule : MBSubModuleBase
    {
        private const string CoreTypeName = "BothPerks.CoreModule";

        private object _core;
        private MethodInfo _onSubModuleLoad;
        private MethodInfo _onNewGameCreated;
        private MethodInfo _onGameLoaded;
        private MethodInfo _onGameStart;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            try
            {
                ApplicationVersion version = ApplicationVersion.FromParametersFile();
                string family = ResolveFamily(version);
                string moduleBin = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string corePath = Path.Combine(moduleBin, "compat", family, "BothPerks.Core.dll");
                if (!File.Exists(corePath))
                {
                    throw new FileNotFoundException("BothPerks core not found for " + version, corePath);
                }

                Assembly coreAssembly = Assembly.LoadFrom(corePath);
                Type coreType = coreAssembly.GetType(CoreTypeName, true);
                _core = Activator.CreateInstance(coreType);
                _onSubModuleLoad = coreType.GetMethod("OnSubModuleLoad", BindingFlags.Instance | BindingFlags.Public);
                _onNewGameCreated = coreType.GetMethod("OnNewGameCreated", BindingFlags.Instance | BindingFlags.Public);
                _onGameLoaded = coreType.GetMethod("OnGameLoaded", BindingFlags.Instance | BindingFlags.Public);
                _onGameStart = coreType.GetMethod("OnGameStart", BindingFlags.Instance | BindingFlags.Public);
                if (_onSubModuleLoad == null || _onNewGameCreated == null || _onGameLoaded == null || _onGameStart == null)
                {
                    throw new MissingMethodException(CoreTypeName, "lifecycle methods");
                }

                Invoke(_onSubModuleLoad);
                Trace.WriteLine("[BothPerks] Loaded " + family + " core for " + version + ".");
            }
            catch (Exception ex)
            {
                Trace.WriteLine("[BothPerks] Bootstrap failed: " + ex);
                _core = null;
            }
        }

        public override void OnNewGameCreated(Game game, object initializerObject)
        {
            base.OnNewGameCreated(game, initializerObject);
            Invoke(_onNewGameCreated, game, initializerObject);
        }

        public override void OnGameLoaded(Game game, object initializerObject)
        {
            base.OnGameLoaded(game, initializerObject);
            Invoke(_onGameLoaded, game, initializerObject);
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarter)
        {
            base.OnGameStart(game, gameStarter);
            Invoke(_onGameStart, game, gameStarter);
        }

        private void Invoke(MethodInfo method, params object[] arguments)
        {
            if (_core == null || method == null)
            {
                return;
            }

            try
            {
                method.Invoke(_core, arguments);
            }
            catch (Exception ex)
            {
                Trace.WriteLine("[BothPerks] Core callback failed: " + ex);
            }
        }

        private static string ResolveFamily(ApplicationVersion version)
        {
            if (version.Major != 1)
            {
                throw new NotSupportedException("Unsupported Bannerlord version: " + version);
            }

            if (version.Minor <= 2)
            {
                return "v1_2";
            }
            if (version.Minor == 3)
            {
                return "v1_3";
            }

            return "v1_4";
        }
    }
}
