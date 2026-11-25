using NUnit.Framework;
using UnityEngine;

public class InputBackendTest
{
    // Every button and both robots use the legacy UnityEngine.Input API.
    // If the project's Active Input Handling excludes the legacy Input Manager,
    // this throws and OnMouseEnter/OnMouseDown are never sent.
    [Test]
    public void LegacyInputManagerIsEnabled()
    {
        Assert.DoesNotThrow(() => { Input.GetMouseButtonDown(0); Input.GetAxis("Horizontal"); Input.GetAxis("Vertical2"); });
    }
}
