using NUnit.Framework;
using UnityEngine;
using KexEdit;
using System.IO;
using KexEdit.UI;

public class ExtrusionTests {
    [Test]
    public void TestDefaultRailConversion() {
        string sourcePath = Path.Combine(Application.streamingAssetsPath, "DefaultRail.obj");
        var sourceMesh = ImportManager.ParseObjFile(sourcePath);
        var expectedMesh = Resources.Load<Mesh>("FallbackRail");

        Assert.IsNotNull(sourceMesh, "Source mesh should exist");
        Assert.IsNotNull(expectedMesh, "Expected processed mesh should exist");

        bool result = ExtrusionMeshConverter.Convert(sourceMesh, out var outputMesh);

        Assert.IsTrue(result, "Conversion should succeed");
    }
}
