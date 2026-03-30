namespace Houtamelo.Spire.Benchmarks.Types;

// ── Physics case types (all unmanaged fields) ──

public record class PhysIdle;
public record class PhysImpulse(float Magnitude);
public record class PhysPosition(float X, float Y);
public record class PhysForce(float FX, float FY, float FZ);
public record class PhysRotation(float X, float Y, float Z, float W);
public record class PhysSpring(float K, float Damping, float Rest, float Min, float Max);
public record class PhysGravity(double G);
public record class PhysCollision(int EntityA, int EntityB);

public union PhysicsNative(PhysIdle, PhysImpulse, PhysPosition, PhysForce, PhysRotation, PhysSpring, PhysGravity, PhysCollision);

// ── Event case types (mixed managed/unmanaged) ──

public record class EvtPoint;
public record class EvtCircle(double Radius);
public record class EvtLabel(string Text);
public record class EvtRectangle(float Width, float Height);
public record class EvtColoredLine(int X1, int Y1, string Color);
public record class EvtTransform(float X, float Y, float Z, float W);
public record class EvtRichText(string Text, int Size, bool Bold, string Font, double Spacing);
public record class EvtError(string Message);

public union EventNative(EvtPoint, EvtCircle, EvtLabel, EvtRectangle, EvtColoredLine, EvtTransform, EvtRichText, EvtError);
