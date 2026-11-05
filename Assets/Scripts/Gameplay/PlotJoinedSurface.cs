using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public sealed class PlotJoinedSurface : MonoBehaviour, IPointerClickHandler,
    IPointerEnterHandler, IPointerExitHandler
{
    private Plot[] members;
    private Action<bool> hoverChanged;
    private Plot hoveredMember;
    private bool pointerInside;

    public void Configure(List<Plot> connected, Action<bool> onHoverChanged)
    {
        members = connected.ToArray();
        hoverChanged = onHoverChanged;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Plot nearest = Nearest(eventData.position);
        if (nearest != null) nearest.OnPointerClick(eventData);
    }

    private void Update()
    {
        if (pointerInside && Mouse.current != null)
            SetHoveredMember(Nearest(Mouse.current.position.ReadValue()));
    }

    private Plot Nearest(Vector2 screenPosition)
    {
        if (members == null || members.Length == 0) return null;
        Vector3 point = transform.position;
        Camera camera = Camera.main;
        if (camera != null)
        {
            float y = GetComponent<Renderer>().bounds.max.y;
            Plane plane = new Plane(Vector3.up, new Vector3(0f, y, 0f));
            Ray ray = camera.ScreenPointToRay(screenPosition);
            if (plane.Raycast(ray, out float distance)) point = ray.GetPoint(distance);
        }
        Plot nearest = null;
        float best = float.MaxValue;
        foreach (Plot plot in members)
        {
            if (plot == null) continue;
            Vector3 delta = plot.transform.position - point;
            float distance = delta.x * delta.x + delta.z * delta.z;
            if (distance >= best) continue;
            best = distance;
            nearest = plot;
        }
        return nearest;
    }

    private void SetHoveredMember(Plot plot)
    {
        if (plot == hoveredMember) return;
        if (hoveredMember != null) hoveredMember.OnPointerExit(null);
        hoveredMember = plot;
        if (hoveredMember != null) hoveredMember.OnPointerEnter(null);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        hoverChanged?.Invoke(true);
        SetHoveredMember(Nearest(eventData.position));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        hoverChanged?.Invoke(false);
        SetHoveredMember(null);
    }

    private void OnDisable() => SetHoveredMember(null);
}
