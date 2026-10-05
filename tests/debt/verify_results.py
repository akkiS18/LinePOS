"""Compare both real implementations against reviewed shared expected outputs."""
import json
import sys
from pathlib import Path

fixtures, csharp, kotlin = [json.loads(Path(p).read_text()) for p in sys.argv[1:]]
expected = {f["id"]: f["expected"] for f in fixtures}
assert len(expected) == len(fixtures), "Duplicate fixture IDs"
assert csharp == expected, "C# results differ from the contract"
assert kotlin == expected, "Kotlin results differ from the contract"
print(f"PARITY PASS: {len(expected)} expected results match in C# and Kotlin")
