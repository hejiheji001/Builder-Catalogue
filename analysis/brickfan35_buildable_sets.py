import collections
import json
import sys
from typing import Dict, Iterable, Tuple

import requests

BASE_URL = "https://d30r5p5favh3z8.cloudfront.net"

USER_USERNAME = "brickfan35"


def load_user_inventory(username: str) -> Dict[Tuple[str, str], int]:
    summary_resp = requests.get(f"{BASE_URL}/api/user/by-username/{username}")
    summary_resp.raise_for_status()
    user_id = summary_resp.json()["id"]

    detail_resp = requests.get(f"{BASE_URL}/api/user/by-id/{user_id}")
    detail_resp.raise_for_status()
    detail = detail_resp.json()

    inventory: Dict[Tuple[str, str], int] = {}
    for entry in detail.get("collection", []):
        piece_id = entry["pieceId"]
        for variant in entry.get("variants", []):
            color = str(variant["color"])
            count = int(variant["count"])
            inventory[(piece_id, color)] = inventory.get((piece_id, color), 0) + count
    return inventory


def load_sets() -> Iterable[Dict]:
    resp = requests.get(f"{BASE_URL}/api/sets")
    resp.raise_for_status()
    return resp.json().get("Sets", [])


def load_set_pieces(set_id: str) -> Iterable[Dict]:
    resp = requests.get(f"{BASE_URL}/api/set/by-id/{set_id}")
    resp.raise_for_status()
    return resp.json().get("pieces", [])


def can_build_set(inventory: Dict[Tuple[str, str], int], pieces: Iterable[Dict]) -> bool:
    required = collections.Counter()
    for item in pieces:
        part = item["part"]
        key = (part["designID"], str(part["material"]))
        required[key] += int(item["quantity"])
    for key, qty in required.items():
        if inventory.get(key, 0) < qty:
            return False
    return True


def main() -> int:
    inventory = load_user_inventory(USER_USERNAME)
    sets = load_sets()

    buildable = []
    for set_info in sets:
        set_id = set_info["id"]
        set_name = set_info["name"]
        pieces = load_set_pieces(set_id)
        if can_build_set(inventory, pieces):
            buildable.append({
                "name": set_name,
                "setNumber": set_info.get("setNumber"),
                "totalPieces": set_info.get("totalPieces"),
            })

    print(json.dumps({
        "username": USER_USERNAME,
        "buildableSets": buildable,
        "count": len(buildable),
    }, indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main())
