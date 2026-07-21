#!/usr/bin/env python3
"""Rewrite an unsigned NuGet package into a byte-stable OPC ZIP archive."""

from __future__ import annotations

import hashlib
import os
import pathlib
import stat
import sys
import tempfile
import xml.etree.ElementTree as ET
import zipfile
from typing import NoReturn


CORE_DIRECTORY = "package/services/metadata/core-properties/"
CORE_NAME = f"{CORE_DIRECTORY}core.psmdcp"
RELATIONSHIPS_NAME = "_rels/.rels"
RELATIONSHIPS_NAMESPACE = (
    "http://schemas.openxmlformats.org/package/2006/relationships"
)
CORE_RELATIONSHIP_TYPE = (
    "http://schemas.openxmlformats.org/package/2006/relationships/metadata/"
    "core-properties"
)
FIXED_TIMESTAMP = (1980, 1, 1, 0, 0, 0)


def fail(message: str) -> NoReturn:
    raise SystemExit(message)


def validate_name(name: str) -> None:
    path = pathlib.PurePosixPath(name)
    if (
        not name
        or "\x00" in name
        or "\\" in name
        or name.startswith("/")
        or any(part in {"", ".", ".."} for part in path.parts)
        or (path.parts and ":" in path.parts[0])
    ):
        fail(f"unsafe NuGet package path: {name!r}")


def canonicalize_relationships(data: bytes, old_core_name: str) -> bytes:
    try:
        root = ET.fromstring(data)
    except ET.ParseError as exc:
        fail(f"invalid NuGet relationship XML: {exc}")

    relationship_tag = f"{{{RELATIONSHIPS_NAMESPACE}}}Relationship"
    if root.tag != f"{{{RELATIONSHIPS_NAMESPACE}}}Relationships":
        fail("NuGet relationship XML has an unexpected root")

    relationships = list(root)
    if not relationships or any(item.tag != relationship_tag for item in relationships):
        fail("NuGet relationship XML contains an unexpected element")

    core_matches = []
    for relationship in relationships:
        relationship_type = relationship.get("Type")
        target = relationship.get("Target")
        if not relationship_type or not target:
            fail("NuGet relationship is missing Type or Target")
        if relationship_type == CORE_RELATIONSHIP_TYPE:
            core_matches.append(relationship)
            if target != f"/{old_core_name}":
                fail("core-properties relationship does not match its package entry")
            relationship.set("Target", f"/{CORE_NAME}")

    if len(core_matches) != 1:
        fail("NuGet package must contain exactly one core-properties relationship")

    for relationship in relationships:
        relationship_type = relationship.get("Type", "")
        target = relationship.get("Target", "")
        digest = hashlib.sha256(
            f"{relationship_type}\0{target}".encode("utf-8")
        ).hexdigest()[:16].upper()
        relationship.set("Id", f"R{digest}")

    root[:] = sorted(
        relationships,
        key=lambda item: (item.get("Type", ""), item.get("Target", "")),
    )
    ET.register_namespace("", RELATIONSHIPS_NAMESPACE)
    return ET.tostring(root, encoding="utf-8", xml_declaration=True)


def canonicalize(package_path: pathlib.Path) -> None:
    if package_path.suffix not in {".nupkg", ".snupkg"}:
        fail("expected a .nupkg or .snupkg path")
    if not package_path.is_file():
        fail(f"NuGet package does not exist: {package_path}")

    original_mode = stat.S_IMODE(package_path.stat().st_mode)
    try:
        with zipfile.ZipFile(package_path, "r") as source:
            if source.comment:
                fail("NuGet package ZIP comments are unsupported")
            entries = source.infolist()
            names = [entry.filename for entry in entries]
            if len(names) != len(set(names)):
                fail("NuGet package contains duplicate ZIP paths")
            for name in names:
                validate_name(name)
            if any(name.casefold() == ".signature.p7s" for name in names):
                fail("signed NuGet packages must not be rewritten")

            core_names = [
                name
                for name in names
                if name.startswith(CORE_DIRECTORY) and name.endswith(".psmdcp")
            ]
            if len(core_names) != 1:
                fail("NuGet package must contain exactly one core-properties entry")
            old_core_name = core_names[0]
            if RELATIONSHIPS_NAME not in names:
                fail("NuGet package is missing _rels/.rels")

            payloads: dict[str, bytes] = {}
            for entry in entries:
                canonical_name = CORE_NAME if entry.filename == old_core_name else entry.filename
                if canonical_name in payloads:
                    fail("NuGet package paths collide after canonicalization")
                payloads[canonical_name] = source.read(entry)
    except (OSError, zipfile.BadZipFile) as exc:
        fail(f"invalid NuGet package: {exc}")

    payloads[RELATIONSHIPS_NAME] = canonicalize_relationships(
        payloads[RELATIONSHIPS_NAME], old_core_name
    )

    temporary_name: str | None = None
    try:
        with tempfile.NamedTemporaryFile(
            prefix=f".{package_path.name}.",
            suffix=".tmp",
            dir=package_path.parent,
            delete=False,
        ) as temporary:
            temporary_name = temporary.name

        with zipfile.ZipFile(
            temporary_name,
            "w",
            compression=zipfile.ZIP_DEFLATED,
            compresslevel=9,
        ) as destination:
            for name in sorted(payloads):
                data = payloads[name]
                info = zipfile.ZipInfo(name, FIXED_TIMESTAMP)
                info.compress_type = zipfile.ZIP_DEFLATED
                info.create_system = 3
                info.external_attr = (0o100644 << 16)
                info.flag_bits = 0x800
                destination.writestr(
                    info,
                    data,
                    compress_type=zipfile.ZIP_DEFLATED,
                    compresslevel=9,
                )

        os.chmod(temporary_name, original_mode)
        os.replace(temporary_name, package_path)
        temporary_name = None
    finally:
        if temporary_name is not None:
            pathlib.Path(temporary_name).unlink(missing_ok=True)


def main() -> None:
    if len(sys.argv) != 2:
        fail("usage: canonicalize-nuget-package.py PACKAGE")
    canonicalize(pathlib.Path(sys.argv[1]))


if __name__ == "__main__":
    main()
