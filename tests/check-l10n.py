import re, os, collections

# Extract first string argument of L.T("...") from C# sources.
def unescape(s):
    return (
        s.replace(r"\\", "\\")
        .replace(r"\"", '"')
        .replace(r"\r", "\r")
        .replace(r"\n", "\n")
        .replace(r"\t", "\t")
    )

calls = []
for root, dirs, files in os.walk("src/NoOccupy"):
    dirs[:] = [d for d in dirs if d not in ("bin", "obj")]
    for fn in files:
        if not fn.endswith(".cs"):
            continue
        path = os.path.join(root, fn)
        text = open(path, encoding="utf-8-sig").read()
        for m in re.finditer(r'L\.T\(\s*"((?:[^"\\]|\\.)*)"', text):
            calls.append((unescape(m.group(1)), path))
        for m in re.finditer(r'NoOccupy\.L\.T\(\s*"((?:[^"\\]|\\.)*)"', text):
            calls.append((unescape(m.group(1)), path))

s = open("src/NoOccupy/Localization.cs", encoding="utf-8").read()
dicts = {}
for name, block in re.findall(
    r"Dictionary<string, string> (\w+) =\s*new Dictionary<string, string>\(StringComparer.Ordinal\)\s*\{(.*?)\n            \};",
    s,
    re.S,
):
    dicts[name] = set(unescape(k) for k in re.findall(r'\{ "((?:[^"\\]|\\.)*)",', block))

for name, keys in dicts.items():
    missing = sorted(set(k for k, _ in calls) - keys)
    print(name, "missing", len(missing))
    for k in missing:
        print("  " + repr(k).encode("unicode_escape").decode("ascii"))
print("calls", len(set(k for k, _ in calls)))
