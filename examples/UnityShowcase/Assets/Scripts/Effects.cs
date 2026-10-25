using System;
using UnityEngine;

namespace Houtamelo.Spire.Showcase {

[Serializable]
public abstract class Effect {
    public abstract int Apply(int health);
}

[Serializable]
public sealed class DamageEffect: Effect {
    [SerializeField] public int amount;

    public DamageEffect() { }
    public DamageEffect(int amount) => this.amount = amount;
    public override int Apply(int health) => Mathf.Max(0, health - amount);
}

[Serializable]
public sealed class HealingEffect: Effect {
    [SerializeField] public int amount;

    public HealingEffect() { }
    public HealingEffect(int amount) => this.amount = amount;
    public override int Apply(int health) => Mathf.Min(100, health + amount);
}

}
