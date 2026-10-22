import re, os

def unescape(s):
    return (
        s.replace(r"\\", "\\")
        .replace(r"\"", '"')
        .replace(r"\r", "\r")
        .replace(r"\n", "\n")
        .replace(r"\t", "\t")
    )

calls = []
for root, dirs, files in os.walk("src/NoOccupy.Setup"):
    dirs[:] = [d for d in dirs if d not in ("bin", "obj")]
    for fn in files:
        if not fn.endswith(".cs"):
            continue
        path = os.path.join(root, fn)
        text = open(path, encoding="utf-8-sig").read()
        for m in re.finditer(r'S\.T\(\s*"((?:[^"\\]|\\.)*)"', text):
            calls.append((unescape(m.group(1)), path))

s = open("src/NoOccupy.Setup/SetupLocalization.cs", encoding="utf-8").read()
dicts = {}
for name, block in re.findall(
    r"Dictionary<string, string> (\w+) =\s*new Dictionary<string, string>\(StringComparer.Ordinal\)\s*\{(.*?)\n            \};",
    s,
    re.S,
):
    raw = re.findall(r'\{ "((?:[^"\\]|\\.)*)",', block)
    dicts[name] = set(unescape(k) for k in raw)
    dups = [k for k, c in __import__("collections").Counter(raw).items() if c > 1]
    print(name, "dups", [d.encode("unicode_escape").decode("ascii") for d in dups])

for name, keys in dicts.items():
    missing = sorted(set(k for k, _ in calls) - keys)
    print(name, "missing", len(missing))
    for k in missing:
        print("  " + repr(k).encode("unicode_escape").decode("ascii"))
print("calls", len(set(k for k, _ in calls)))
