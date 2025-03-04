// RUN: %dafny_0 /env:0 /rprint:- "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

lemma TestBool(F: bool, T: bool)
  requires F==false && T==true
{
  calc {  // <==
    false;
    T;  // error: this step does not hold
    F;  // note: this step holds with main operator ==>
  }
  calc {  // ==>
    ((true));
    F;  // error: this step does not hold
    T;  // note: this step holds with main operator ==>
  }
  calc {  // ==>
    ((((F))));
    T;  // note: this step holds with main operator ==>
    (false);  // error: this step does not hold
  }
  calc {  // <==
    false;
    T;  // error: this step does not hold
    F;  // note: this step holds with main operator <==
  }
}

lemma TestSet(Empty: set<int>, Nonempty: set<int>)
  requires |Empty| == 0 && |Nonempty| > 0
{
  calc {  // >=
    {};
    Nonempty;  // error: this step does not hold
    Empty;  // note: this step holds with main operator >=
  }
  calc {  // <=
    Empty;
    Nonempty;  // note: this step holds with main operator <=
    ({});  // error: this step does not hold
  }
}

lemma TestMultiset(Empty: multiset<int>, Nonempty: multiset<int>)
  requires |Empty| == 0 && |Nonempty| > 0
{
  calc {  // >=
    ((((multiset{}))));
    Nonempty;  // error: this step does not hold
    Empty;  // note: this step holds with main operator >=
  }
  calc {  // <=
    Empty;
    Nonempty;  // note: this step holds with main operator <=
    multiset{};  // error: this step does not hold
  }
}

// Print test for /rprint. Note, this same class is tested with /dprint in Test/dafny0/Twostate-Resolution.dfy.
module PrintTest {
  function method Five(): int { 5 }
  function Six(): int { 6 }

  function method Ten(): int {
    var f := Five();
    ghost var s := Six();
    assert s == 6;
    f + f
  }

  function method TenAgain(): int {
    var ten :=
      var f := Five();
      ghost var s := Six();
      assert s == 6;
      f + f;
    ten
  }

  function TenOnceMore(): int {
    var ten :=
      var f := Five();
      ghost var s := Six();
      assert s == 6;
      f + f;
    ten
  }

  function Eleven(): int {
    var f, s := Five(), Six();
    f + s
  }

  function Twelve(): int {
    var s, t := Six(), Six();
    s + t
  }

  function method Twenty(): int {
    var x :| x == 10;
    x + x
  }

  function method TwentyOne(): int {
    ghost var x :| x == 10 && Yes(x);
    assert x + x + 1 == 21;
    21
  }

  predicate Yes(x: int) { true }

  type Odd = x |
    var rr := 2; x % rr == 1
    witness var ww := 2; ww + 7

  newtype NewOdd = x |
    var rr := 2; x % rr == 1
    witness var ww := 2; ww + 7

  type Even = x |
    var rr := 2; x % rr == 0
    ghost witness var ww := 2; ww + 8

  newtype NewEven = x |
    var rr := 2; x % rr == 0
    ghost witness var ww := 2; ww + 8
}
