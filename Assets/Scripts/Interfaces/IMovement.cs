using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IMovement
{
    Vector2 LookDirection { get; }
    Vector2 MoveDirection { get; }
}

