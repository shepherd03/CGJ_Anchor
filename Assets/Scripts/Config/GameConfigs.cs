using System;
using Luban;
using YokiFrame;

namespace Anchor.Config
{
    public static class GameConfigs
    {
        private const string ResourceRoot = "Config/Luban/Bin";

        private static Tables sTables;

        public static bool IsLoaded => sTables != null;

        public static Tables Tables => Load();

        public static Tables Load(bool forceReload = false)
        {
            if (sTables != null && !forceReload)
            {
                return sTables;
            }

            sTables = new Tables(LoadTableBytes);
            return sTables;
        }

        public static void Unload()
        {
            sTables = null;
        }

        private static ByteBuf LoadTableBytes(string fileName)
        {
            var resourcePath = $"{ResourceRoot}/{fileName}";
            byte[] bytes;

            try
            {
                bytes = ResKit.LoadRaw(resourcePath);
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is NotSupportedException)
            {
                throw new InvalidOperationException(
                    "YokiFrame ResKit is not ready. Add YokiFrame.Unity.UnityBootstrap to the scene before loading Luban configs.",
                    exception);
            }

            if (bytes == null || bytes.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Luban config data not found or empty: Resources/{resourcePath}.bytes");
            }

            return new ByteBuf(bytes);
        }
    }
}
