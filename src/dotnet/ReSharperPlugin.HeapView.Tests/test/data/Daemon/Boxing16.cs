interface I {
  int Prop { get; set; }
  void Method();
  int this[int index] { get; set; }
}

struct S : I {
  public int Prop { get; set; }
  public void Method() { }
  public int this[int index] { get => 0; set { } }
    
  public void M<T, U>(S s, T t, U u) where T : struct {
    ((I) s).Prop += 42;
    ((I) t).Prop = 424;
    _ = ((I) u).Prop;

    ((I) s).Method();
    ((I) t).Method();
    ((I) u).Method();

    ((I) s)[42] += 123;
    ((I) t)[42] += 123;
    ((I) u)[42] += 123;

    System.GC.KeepAlive((I) s); // box
  }
}