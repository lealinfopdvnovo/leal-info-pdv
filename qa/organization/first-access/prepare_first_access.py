"""Integrate the first-access screen on the compiled 37 source."""
from pathlib import Path
import shutil

root = Path("candidate")
program = root / "Program.cs"
source = program.read_text(encoding="utf-8-sig")
marker = "            PetRuntime.Initialize(Licensing.InstallationLicense.Current?.ClientCode ?? \"\");"
assert source.count(marker) == 1, "Unexpected startup implementation"
source = source.replace(marker, """            if (Network.NetworkConfiguration.Current.Mode != "terminal" &&
                !argv.Contains("--manutencao") && !argv.Contains("--update-transaction") &&
                Auth.UserCount() == 0)
            {
                using var firstAccessSetup = new FirstAccessSetup();
                if (firstAccessSetup.ShowDialog() != DialogResult.OK) return;
                Database.Initialize();
            }
""" + marker, 1)
program.write_text(source, encoding="utf-8")
shutil.copyfile(Path(__file__).with_name("FirstAccessSetup.cs.source"), root / "FirstAccessSetup.cs")
