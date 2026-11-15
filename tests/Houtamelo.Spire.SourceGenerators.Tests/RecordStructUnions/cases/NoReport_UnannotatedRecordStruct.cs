//@ should_pass
// An unannotated record struct is unrelated to discriminated-union generation.
public record struct PlainValue(int amount);
