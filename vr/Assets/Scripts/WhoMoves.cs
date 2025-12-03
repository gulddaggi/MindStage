using UnityEngine;
public class WhoMoves : MonoBehaviour
{
    Transform t; Vector3 last;
    void Awake() { t = transform; last = t.position; LogChain(); }
    void Update()
    {
        if (t.position != last)
        {
            Debug.Log($"[WhoMoves] {name} moved to {t.position} (parent={t.parent?.name})");
            last = t.position;
        }
    }
    void LogChain()
    {
        var p = t; string chain = "";
        while (p != null) { chain = p.name + (chain == "" ? "" : " -> ") + chain; p = p.parent; }
        Debug.Log($"[WhoMoves] chain: {chain}");
    }
}
