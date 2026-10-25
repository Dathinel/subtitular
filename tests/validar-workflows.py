import glob
import yaml

files = sorted(glob.glob(".github/workflows/*.yml"))
assert len(files) > 0, "No se encontraron archivos en .github/workflows/"

for path in files:
    with open(path, "r", encoding="utf-8") as f:
        data = yaml.safe_load(f)
    triggers = list(data.get("on", {}).keys()) if isinstance(data.get("on"), dict) else data.get("on")
    print(f"{path}: disparadores = {triggers}")
