using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CableRenderer : MonoBehaviour
{
    [SerializeField] private JobCableComponent _cableComponent;
    [SerializeField] private LineRenderer _lineRenderer;
    [SerializeField] private float _lineWidth;

    private void Start()
    {
        _lineRenderer.positionCount = _cableComponent.Segments + 1;
    }

    private void LateUpdate()
    {
        _lineRenderer.SetPositions(_cableComponent.Positions);
    }

    private void OnValidate()
    {
        _lineRenderer.startWidth = _lineWidth;
        _lineRenderer.endWidth = _lineWidth;
    }
}
