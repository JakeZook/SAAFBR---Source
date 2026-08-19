using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IUseable
{
    void Use();
    Quaternion GetRotationOffset();
    Vector3 GetTransformOffset();
}
