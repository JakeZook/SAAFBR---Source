using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TempChecker : MonoBehaviour
{
    [SerializeField] private Material raw;
    [SerializeField] private Material rare;
    [SerializeField] private Material medium;
    [SerializeField] private Material well;
    [SerializeField] private Material burnt;
    [SerializeField] private Material reset;
    private MeshRenderer mesh;

    private void Start()
    {
        mesh = GetComponent<MeshRenderer>();
    }

    private void OnCollisionEnter(Collision other) {
        Patty patty = other.collider.GetComponent<Patty>();
        if (patty)
        {
            Patty.PattyTempState top = patty.topTempState;
            Patty.PattyTempState bottom = patty.bottomTempState;

            if (top == Patty.PattyTempState.Raw || bottom == Patty.PattyTempState.Raw) mesh.material = raw;
            if (top == Patty.PattyTempState.Rare || bottom == Patty.PattyTempState.Rare) mesh.material = rare;
            if (top == Patty.PattyTempState.Medium || bottom == Patty.PattyTempState.Medium) mesh.material = medium;
            if (top == Patty.PattyTempState.Well || bottom == Patty.PattyTempState.Well) mesh.material = well;
            if (top == Patty.PattyTempState.Burnt || bottom == Patty.PattyTempState.Burnt) mesh.material = burnt;
        }
    }
}
