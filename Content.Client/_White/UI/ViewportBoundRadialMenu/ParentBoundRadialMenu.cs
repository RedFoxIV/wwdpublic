using Content.Client.UserInterface.Controls;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Content.Client._White.UI.ViewportBoundRadialMenu;

[Virtual]
public class ParentBoundRadialMenu : RadialMenu
{
    protected readonly MainViewport _mainViewport;

    public ParentBoundRadialMenu(Control parent) : base()
    {
        _mainViewport = _uiManager.ActiveScreen?.GetWidget<MainViewport>()!; // if this throws you've got bigger problems than a single "!".
        parent.AddChild(this);
    }

    public Vector2 FullRot(bool antiClockwise) =>
        antiClockwise ? new Vector2(MathF.Tau - float.Epsilon, 0) : new Vector2(0, MathF.Tau - float.Epsilon);

    public void CalculateAngularRange(RadialContainer currentRadialContainer, Vector2 center, float radius, float screenWidth, float screenHeight, int childCount, bool antiClockwise)
    {
        if (childCount <= 1)
        { 
            currentRadialContainer.AngularRange = FullRot(antiClockwise);
            return;
        }
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


        // Sort angles and remove duplicates
        if (intersectionAngles.Count == 0)
        {
            currentRadialContainer.AngularRange = FullRot(antiClockwise);
            return;
        }

        intersectionAngles.Sort();
        intersectionAngles.Add(intersectionAngles[0]+MathF.Tau); // include wrap-around arc (from last to first)
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
        float maxMidAngle = 0;

        float ang(float rad) => rad * 180f / MathF.PI;

        // Check each consecutive interval
        for (int i = 1; i < intersectionAngles.Count; i++)
        {
            float start = intersectionAngles[i - 1];
            float end = intersectionAngles[i];
            float length = end - start;

            float astart = ang(start);
            float aend = ang(end);
            float alen = ang(length);

            // Check midpoint of the interval
            float midAngle = (start + end) / 2;
            float xMid = center.X + radius * MathF.Cos(midAngle - MathF.PI / 2);
            float yMid = center.Y + radius * MathF.Sin(midAngle - MathF.PI / 2);

            float amidAngle = ang(midAngle);

            if (xMid >= 0 && xMid <= screenWidth && yMid >= 0 && yMid <= screenHeight)
            {
                if (length > maxArcLength)
                {
                    maxArcLength = length;
                    bestStart = start;
                    bestEnd = end;
                    maxMidAngle = midAngle;
                }
            }
        }

        // a shitty way to make the arc smaller the closer radialmenu is to the parent's edge
        //var decrease = (1 - (maxArcLength / MathF.Tau)) * (MathF.PI / 2);
        //bestStart += decrease;
        //bestEnd -= decrease;    


        var newArc = maxArcLength;
        if(newArc > MathF.PI / 2 && newArc <= MathF.Tau - 5 * MathF.PI / 180f)
        {
            newArc = newArc / 3f + MathF.PI / 3;
        }

        if (childCount == 2)
            newArc *= 0.5f;

        bestStart = maxMidAngle - newArc / 2;
        bestEnd = maxMidAngle + newArc / 2;


        if (antiClockwise ^ bestStart > bestEnd) // the above can invert our arc if it's a bit below pi/2, so we fix it if needed
            (bestStart, bestEnd) = (bestEnd, bestStart);
        
        //// make the arc smaller if there are only two children because it looks a bit nicer
        //if(childCount == 2)
        //{
        //    bestStart += maxArcLength * 0.15f;
        //    bestEnd   -= maxArcLength * 0.15f;
        //}
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
        CalculateAngularRange(radCont, _cachedPosition.Value, radCont.Radius + 32, Parent.Size.X, Parent.Size.Y, radCont.ChildCount, antiCockwise);
    }
}
