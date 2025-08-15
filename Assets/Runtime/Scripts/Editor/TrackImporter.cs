using System.IO;
using UnityEngine;
using UnityEditor.AssetImporters;

namespace KexEdit.Editor {
    [ScriptedImporter(1, "kex")]
    public class TrackImporter : ScriptedImporter {
        public override void OnImportAsset(AssetImportContext ctx) {
            byte[] kexData = File.ReadAllBytes(ctx.assetPath);

            var track = ScriptableObject.CreateInstance<Track>();
            track.Data = kexData;

            string fileName = Path.GetFileNameWithoutExtension(ctx.assetPath);
            var gameObject = new GameObject(fileName);
            var loader = gameObject.AddComponent<CoasterLoader>();

            loader.Track = track;

            ctx.AddObjectToAsset("track", track);
            ctx.AddObjectToAsset("main", gameObject);
            ctx.SetMainObject(gameObject);
        }
    }
}
