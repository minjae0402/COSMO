using UnityEngine;

[DisallowMultipleComponent]
public sealed class CursorLockController : MonoBehaviour
{
    private void OnEnable()
    {
        LockCursor();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            LockCursor();
        }
    }

    private void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
