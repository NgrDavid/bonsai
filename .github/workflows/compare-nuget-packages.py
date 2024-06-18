#!/usr/bin/env python3
import hashlib
import os
import re
import sys

from pathlib import Path
from zipfile import ZipFile, ZipInfo

import gha

# Symbol packages will change even for changes that we don't care about because the deterministic hash embedded in the PDB
# is affected by the MVID of a package's dependencies. We don't want to release a new package when the only things that
# changed were external to the package, so we don't check them.
CHECK_SYMBOL_PACKAGES = False

if len(sys.argv) != 4:
    gha.print_error('Usage: compare-nuget-packages.py <previous-dummy-packages-path> <next-dummy-packages-path> <release-packages-path>')
    sys.exit(1)
else:
    previous_packages_path = Path(sys.argv[1])
    next_packages_path = Path(sys.argv[2])
    release_packages_path = Path(sys.argv[3])

if not previous_packages_path.exists():
    gha.print_error(f"Previous packages path '{previous_packages_path}' doest not exist.")
if not next_packages_path.exists():
    gha.print_error(f"Next packages path '{next_packages_path}' doest not exist.")
if not release_packages_path.exists():
    gha.print_error(f"Release packages path '{previous_packages_path}' doest not exist.")
gha.fail_if_errors()

def verbose_log(message: str):
    print(message)

def should_ignore(file: ZipInfo) -> bool:
    # Ignore metadata files which change on every pack
    if file.filename == '_rels/.rels':
        return True
    if file.filename.startswith('package/services/metadata/core-properties/') and file.filename.endswith('.psmdcp'):
        return True
    
    # Don't care about explicit directories
    if file.is_dir():
        return True
    
    return False

def nuget_packages_are_equivalent(a_path: Path, b_path: Path, is_snupkg: bool = False) -> bool:
    verbose_log(f"Comparing '{a_path}' and '{b_path}'")

    # One package exists and the other does not
    if a_path.exists() != b_path.exists():
        verbose_log(f"Not equivalent: Only one package actually exists")
        return False
    
    # The package doesn't exist at all, assume mistake unless we're checking the optional symbol packages
    if not a_path.exists():
        if is_snupkg:
            verbose_log("Equivalent: Neither package exists")
            return True
        raise FileNotFoundError(f"Neither package exists: '{a_path}' or '{b_path}'")
    
    # From this point on: Check everything and emit messages for debugging purposes
    is_equvalent = True

    # Check if corresponding symbol packages are equivalent
    if CHECK_SYMBOL_PACKAGES and not is_snupkg:
        if not nuget_packages_are_equivalent(a_path.with_suffix(".snupkg"), b_path.with_suffix(".snupkg"), True):
            verbose_log("Not equivalent: Symbol packages are not equivalent")
            is_equvalent = False
        else:
            verbose_log("Symbol packages are equivalent")

    # Compare the contents of the packages
    # NuGet package packing is unfortunately not fully deterministic so we cannot compare the packages directly
    # https://github.com/NuGet/Home/issues/8601
    with ZipFile(a_path, 'r') as a_zip, ZipFile(b_path, 'r') as b_zip:
        b_infos = { }
        for b_info in b_zip.infolist():
            if should_ignore(b_info):
                continue
            assert b_info.filename not in b_infos
            b_infos[b_info.filename] = b_info

        for a_info in a_zip.infolist():
            if should_ignore(a_info):
                continue

            b_info = b_infos.pop(a_info.filename, None)
            if b_info is None:
                verbose_log(f"Not equivalent: '{a_info.filename}' exists in '{a_path}' but not in '{b_path}'")
                is_equvalent = False
                continue

            if a_info.CRC != b_info.CRC:
                verbose_log(f"Not equivalent: CRCs of '{a_info.filename}' do not match between '{a_path}' and '{b_path}'")
                is_equvalent = False
                continue
            
            if a_info.file_size != b_info.file_size:
                verbose_log(f"Not equivalent: File sizes of '{a_info.filename}' do not match between '{a_path}' and '{b_path}'")
                is_equvalent = False
                continue

            a_hash = hashlib.file_digest(a_zip.open(a_info), 'sha256').hexdigest() # type: ignore
            b_hash = hashlib.file_digest(b_zip.open(b_info), 'sha256').hexdigest() # type: ignore
            if a_hash != b_hash:
                verbose_log(f"Not equivalent: SHA256 hashes of '{a_info.filename}' do not match between '{a_path}' and '{b_path}'")
                is_equvalent = False
                continue

        # Ensure every file in B was processed
        if len(b_infos) > 0:
            is_equvalent = False
            verbose_log(f"Not equivalent: The following file(s) exist in '{a_path}' but not in '{b_path}'")
            for filename in b_infos:
                verbose_log(f"  '{filename}'")

    return is_equvalent

package_file_name_regex = re.compile(r"^(?P<package_name>.+?)\.(?P<major>0|[1-9]\d*)\.(?P<minor>0|[1-9]\d*)\.(?P<patch>0|[1-9]\d*)(?:-(?P<prerelease>(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+(?P<buildmetadata>[0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?\.nupkg$")
def get_package_name(file_name: str) -> str:
    match = package_file_name_regex.match(file)
    if match is None:
        gha.print_warning(f"File name '{file_name}' does not match the expected format for a NuGet package.")
        return file_name
    return match.group('package_name')

different_packages = []
next_packages = set()
for file in os.listdir(next_packages_path):
    if not file.endswith(".nupkg"):
        continue

    package_name = get_package_name(file)
    next_packages.add(package_name)

    if not nuget_packages_are_equivalent(next_packages_path / file, previous_packages_path / file):
        verbose_log(f"'{file}' differs")
        different_packages.append(file)

release_packages = set()
for file in os.listdir(release_packages_path):
    if file.endswith(".nupkg"):
        release_packages.add(get_package_name(file))

print()
print("The following packages have changes:")
for package in different_packages:
    print(f"  {package}")

# Ensure the next dummy reference and release package sets contain the same packages
def list_missing_peers(error_message: str, packages: set[str]):
    if len(packages) == 0:
        return
    
    print()
    gha.print_error(error_message)
    for package in packages:
        gha.print_error(f"  {package}")

list_missing_peers("The following packages exist in the release package artifact, but not in the next dummy reference artifact:", release_packages - next_packages)
list_missing_peers("The following packages exist in the next dummy reference artifact, but not in the release package artifact:", next_packages - release_packages)

gha.fail_if_errors()
