using Content.Client.UserInterface.Controls;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Content.Client._White.UI.ViewportBoundRadialMenu;

[Virtual]
public class ParentBoundRadialMenu : RadialMenu
{
    protected readonly MainViewport _mainViewport;
    public ParentBoundRadialMenu() : base()
    {
        _mainViewport = _uiManager.ActiveScreen?.GetWidget<MainViewport>()!; // if this throws you've got bigger problems than a single "!".
    }

    public Vector2 FullRot(bool antiClockwise) =>
        antiClockwise ? new Vector2(MathF.Tau - float.Epsilon, 0) : new Vector2(0, MathF.Tau - float.Epsilon);

    public void CalculateAngularRange(RadialContainer currentRadialContainer, Vector2 center, float radius, float screenWidth, float screenHeight, bool antiClockwise)
    {
        List<float> intersectionAngles = new List<float>();

        // Calculate intersections with left edge (x = 0)
        if (radius > center.X)
        {
            float cosTheta = center.X / radius;
            if (MathF.Abs(cosTheta) <= 1)
            {
                float theta = MathF.Acos(cosTheta);
                intersectionAngles.Add(1.5f * MathF.PI - theta);
                intersectionAngles.Add(1.5f * MathF.PI + theta);
            }
        }

        // Right edge (x = screenWidth)
        float dxRight = screenWidth - center.X;
        if (radius > dxRight)
        {
            float cosTheta = dxRight / radius;
            if (MathF.Abs(cosTheta) <= 1)
            {
                float theta = MathF.Acos(cosTheta);
                intersectionAngles.Add(theta + MathF.PI / 2);
                intersectionAngles.Add(MathF.Tau - theta + MathF.PI / 2);
            }
        }

        // Top edge (y = 0)
        if (radius > center.Y)
        {
            float sinTheta = center.Y / radius;
            if (MathF.Abs(sinTheta) <= 1)
            {
                float thetaAsin = MathF.Acos(sinTheta);
                float theta1 = thetaAsin < 0 ? thetaAsin + MathF.Tau : thetaAsin;
                float theta2 = MathF.Tau - thetaAsin;
                theta2 = theta2 < 0 ? theta2 + MathF.Tau : theta2;
                intersectionAngles.Add(theta1);
                intersectionAngles.Add(theta2);
            }
        }

        // Bottom edge (y = screenHeight)
        float dyBottom = screenHeight - center.Y;
        if (radius > dyBottom)
        {
            float sinTheta = dyBottom / radius;
            if (MathF.Abs(sinTheta) <= 1)
            {
                float thetaAsin = MathF.Acos(sinTheta);
                float theta1 = thetaAsin < 0 ? thetaAsin + MathF.Tau : thetaAsin;
                float theta2 = MathF.PI - thetaAsin;
                theta2 = theta2 < 0 ? theta2 + MathF.Tau : theta2;
                intersectionAngles.Add(MathF.PI - theta1);
                intersectionAngles.Add(MathF.PI + theta1);
            }
        }


        //// Add 0 and Tau to the list to cover full circle
        //intersectionAngles.Add(0);
        //intersectionAngles.Add(MathF.Tau);
        // Sort angles and remove duplicates
        if (intersectionAngles.Count == 0)
        {
            currentRadialContainer.AngularRange = FullRot(antiClockwise);
            return;
        }

        intersectionAngles.Sort();
        intersectionAngles.Add(intersectionAngles[0]+MathF.Tau);
        for (int i = intersectionAngles.Count - 1; i > 0; i--)
        {
            if (MathF.Abs(intersectionAngles[i] - intersectionAngles[i - 1]) < 1e-5f)
            {
                intersectionAngles.RemoveAt(i);
            }
        }

        float maxArcLength = 0;
        float bestStart = 0;
        float bestEnd = MathF.Tau;

        // Check each consecutive interval
        for (int i = 1; i < intersectionAngles.Count; i++)
        {
            float start = intersectionAngles[i - 1];
            float end = intersectionAngles[i];
            float length = end - start;

            // Check midpoint of the interval
            float midAngle = (start + end) / 2 - MathF.PI/2;
            float xMid = center.X + radius * MathF.Cos(midAngle);
            float yMid = center.Y + radius * MathF.Sin(midAngle);

            if (xMid >= 0 && xMid <= screenWidth && yMid >= 0 && yMid <= screenHeight)
            {
                if (length > maxArcLength)
                {
                    maxArcLength = length;
                    bestStart = start;
                    bestEnd = end;
                }
            }
        }

        //// Check wrap-around interval (last angle to first angle plus Tau)
        //if (intersectionAngles.Count > 0)
        //{
        //    float wrapStart = intersectionAngles[intersectionAngles.Count - 1];
        //    float wrapEnd = intersectionAngles[0];
        //    float wrapLength = wrapStart - wrapEnd;
        //
        //    float midWrap = (wrapStart + wrapEnd) / 2 - 1.5f * MathF.PI;
        //    float xWrap = center.X + radius * MathF.Cos(midWrap);
        //    float yWrap = center.Y + radius * MathF.Sin(midWrap);
        //
        //    if (xWrap >= 0 && xWrap <= screenWidth && yWrap >= 0 && yWrap <= screenHeight)
        //    {
        //        if (wrapLength > maxArcLength)
        //        {
        //            maxArcLength = wrapLength;
        //            bestStart = wrapStart;
        //            bestEnd = wrapEnd;
        //        }
        //    }
        //}

        maxArcLength = 1 - (maxArcLength / MathF.Tau);
        bestStart += maxArcLength;
        bestEnd -= maxArcLength;    

        if (antiClockwise)
            (bestStart, bestEnd) = (bestEnd, bestStart);
        currentRadialContainer.AngularRange = new Vector2(bestStart, bestEnd);
    }

    private Vector2? _cachedPosition;
    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (Parent is null) 
            return;
        Vector2 boundPos = Position + Size / 2;

        if (_cachedPosition == boundPos ||
            GetCurrentActiveLayer() is not RadialContainer radCont) // breaks badly if childCount is one.
            return;

        _cachedPosition = boundPos;
        bool antiCockwise = radCont.RadialAlignment == RadialContainer.RAlignment.AntiClockwise; // sic
        if (radCont.ChildCount >= 2)
            CalculateAngularRange(radCont, _cachedPosition.Value, radCont.Radius + 64, Parent.Size.X, Parent.Size.Y, antiCockwise);
        else
            radCont.AngularRange = FullRot(antiCockwise);
    }
}
