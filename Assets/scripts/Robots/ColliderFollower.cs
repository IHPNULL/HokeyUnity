using UnityEngine;

// A tool part's collider lives on the flat chassis (not the tilting body). Copies the part's
// local rotation once it has been set by the builder (the frame after creation).
public class ColliderFollower : MonoBehaviour
{
    Transform col; int frames;
    public void Init(Transform c) { col = c; }
    void LateUpdate()
    {
        if (col == null) { enabled = false; return; }
        col.localRotation = transform.localRotation;
        if (++frames > 1) enabled = false;
    }
}
