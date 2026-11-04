#!/usr/bin/env python3
"""Generates the Suflae sharing combination programs (C175 step 3).

Each program has a Work and the main task update a count through a handle that was stored into something
the Work already shares, and print the count. The axes:

  site   where the store happens: start, a routine, a routine called under `try`, inside a Work
  dest   what receives it: an entity member variable, List, Dict, Set, SortedDict, SortedSet, SortedList
  value  what is stored: a handle, a record, a tuple, a variant, nested values, an entity holding a handle
  wait   none, or a wait that sits in the same statement as an entity access (see WAITS)

usage: generate.py OUTDIR [--site S] [--dest D] [--value V] [--rounds N] [--only substring]
Every case is a folder OUTDIR/<name>/ with main.sf and config.toml (debug mode, ThreadSanitizer on).
"""
import argparse
import os
import sys

SITES = ["start", "routine", "try", "work"]
DESTS = ["field", "list", "dict", "set", "sorteddict", "sortedset", "sortedlist"]
VALUES = ["handle", "record", "tuple", "variant", "nested", "chain"]
ORDERED_ELEMENTS = ["set", "sortedset", "sortedlist"]
WAITS = ["retrieve_local", "retrieve_list", "gather", "mailbox", "channel"]

# What the value kinds declare, and how a value is made and how its Box is reached.
VALUE_DECLS = {
    "handle": ("Box", ""),
    "record": ("Slot", "record Slot\nobeys Hashable\nobeys Comparable\n    box: Box\n    tag: S64\n"),
    "tuple": ("(Box, S64)", ""),
    "variant": ("Cargo", "variant Cargo\n    Box\n    Text\n"),
    "nested": ("Outer", "record Slot\nobeys Hashable\nobeys Comparable\n    box: Box\n    tag: S64\n\nrecord Outer\n    pair: (Slot, S64)\n"),
    "chain": ("Wrap", "entity Wrap\nobeys Hashable\nobeys Comparable\n    box: Box\n"),
}
VALUE_MAKE = {
    "handle": "return Box(n: 0)",
    "record": "return Slot(box: Box(n: 0), tag: 1)",
    "tuple": "return (Box(n: 0), 1)",
    "variant": "return Box(n: 0)",
    "nested": "return Outer(pair: (Slot(box: Box(n: 0), tag: 1), 2))",
    "chain": "return Wrap(box: Box(n: 0))",
}
VALUE_DUMMY = {
    "handle": "Box(n: -1)",
    "record": "Slot(box: Box(n: -1), tag: 0)",
    "tuple": "(Box(n: -1), 0)",
    "variant": "Box(n: -1)",
    "nested": "Outer(pair: (Slot(box: Box(n: -1), tag: 0), 0))",
    "chain": "Wrap(box: Box(n: -1))",
}
VALUE_BOX_BODY = {
    "handle": "return e",
    "record": "return e.box",
    "tuple": "return e.item0",
    "variant": "when e\n        is Box as b => return b\n        else => throw AbsentValueError()",
    "nested": "return e.pair.item0.box",
    "chain": "return e.box",
}

# Containers: (type text, construction, import, store statement(s) taking `v`, has-test, peek body).
DEST = {
    "field": dict(
        decl="entity Holder\n    slot: {V}\n    ready: Bool\n",
        type="Holder", make="Holder(slot: {DUMMY}, ready: false)", imports="",
        store=["shared.slot = v", "shared.ready = true"],
        has="shared.ready",
        peek="return box_of(e: shared.slot)"),
    "list": dict(decl="", type="List[{V}]", make="List[{V}]()", imports="",
        store=["shared.add_last(value: v)"], has="shared.count() > 0",
        peek="return box_of(e: shared[0])"),
    "dict": dict(decl="", type="Dict[S64, {V}]", make="Dict[S64, {V}]()", imports="",
        store=["shared[1] = v"], has="shared.has_key(1)",
        peek="return box_of(e: shared[1])"),
    "set": dict(decl="", type="Set[{V}]", make="Set[{V}]()", imports="",
        store=["shared.add(value: v)"], has="shared.count() > 0",
        peek="for e in shared\n        return box_of(e: e)\n    throw AbsentValueError()"),
    "sorteddict": dict(decl="", type="SortedDict[S64, {V}]", make="SortedDict[S64, {V}]()", imports="import Collections\n",
        store=["shared[1] = v"], has="shared.count() > 0",
        peek="return box_of(e: shared[1])"),
    "sortedset": dict(decl="", type="SortedSet[{V}]", make="SortedSet[{V}]()", imports="import Collections\n",
        store=["shared.add(value: v)"], has="shared.count() > 0",
        peek="return box_of(e: shared.first())"),
    "sortedlist": dict(decl="", type="SortedList[{V}]", make="SortedList[{V}]()", imports="import Collections\n",
        store=["shared.add(value: v)"], has="shared.count() > 0",
        peek="return box_of(e: shared.first())"),
}


def indent(lines, n):
    pad = " " * n
    return "\n".join(pad + l if l else l for l in lines)


def program(site, dest, value, rounds):
    vt, vdecl = VALUE_DECLS[value]
    d = DEST[dest]
    fmt = lambda s: s.format(V=vt, DUMMY=VALUE_DUMMY[value])
    ct = fmt(d["type"])
    parts = []
    parts.append("import IO/Console\n" + d["imports"])
    parts.append("entity Box\nobeys Hashable\nobeys Comparable\n    n: S64\n")
    if vdecl:
        parts.append(vdecl)
    if d["decl"]:
        parts.append(fmt(d["decl"]))
    parts.append(f"routine make() -> {vt}\n    {VALUE_MAKE[value]}\n")
    parts.append(f"routine box_of(e: {vt}) -> Box\n    {VALUE_BOX_BODY[value]}\n")
    parts.append(f"routine peek(shared: {ct}) -> Box\n    {d['peek']}\n")
    has = d["has"]
    stores = "\n".join("    " + s for s in d["store"])
    parts.append(f"routine store(shared: {ct}, v: {vt}, fail: Bool) -> S64\n    if fail\n        throw VerificationFailedError()\n{stores}\n    return 0\n")
    # The reader side: waits for the stored value and bumps through the shared thing each round.
    parts.append(f"""routine reader(shared: {ct}, rounds: S64) -> S64
    until {has}
        waitfor(1ms)
    for _ in 1 to rounds
        var b = peek(shared: shared)
        b.n += 1
    return rounds
""")
    parts.append(f"routine put(shared: {ct}) -> Box\n    var v = make()\n    var box = box_of(e: v)\n{stores}\n    return box\n")
    # The writer Work: makes the value, stores it, bumps through its own handle.
    parts.append(f"""routine producer(shared: {ct}, rounds: S64) -> S64
    var v = make()
    var box = box_of(e: v)
{stores}
    for _ in 1 to rounds
        box.n += 1
    return rounds
""")
    parts.append(f"""routine bump(box: Box, rounds: S64)
    for _ in 1 to rounds
        box.n += 1
    return
""")
    body = [f"var rounds: S64 = {rounds}", f"var shared = {fmt(d['make'])}"]
    if site == "work":
        body += [
            "var job = producer.to[Work](shared: shared, rounds: rounds)",
            "job.start_now()",
            f"until {has}",
            "    waitfor(1ms)",
            "for _ in 1 to rounds",
            "    var b = peek(shared: shared)",
            "    b.n += 1",
            "job.retrieve()",
        ]
    else:
        body += ["var job = reader.to[Work](shared: shared, rounds: rounds)", "job.start_now()"]
        if site == "start":
            body += ["var v = make()", "var box = box_of(e: v)"] + [s.strip() for s in d["store"]]
            body += ["bump(box: box, rounds: rounds)"]
        elif site == "routine":
            body += ["var box = put(shared: shared)", "bump(box: box, rounds: rounds)"]
        else:  # try
            body += ["var v = make()", "var box = box_of(e: v)", "var stored = try store(shared: shared, v: v, fail: false)",
                     "bump(box: box, rounds: rounds)"]
        body += ["job.retrieve()"]
    body += [
        "var total = peek(shared: shared).n",
        'if total == 2 * rounds',
        '    show("RESULT ok")',
        'else',
        '    show(f"RESULT bad got {total} expected {2 * rounds}")',
    ]
    parts.append("routine start()\n" + indent(body, 4) + "\n    return\n")
    return "\n".join(parts)


def wait_program(wait):
    """A wait in the same statement as an entity access: the Work needs the holder to finish."""
    head = """import IO/Console

entity Holder
    x: S64

routine touch(holder: Holder) -> S64
    holder.x += 1
    return holder.x
"""
    if wait == "retrieve_local":
        return head + """
routine start()
    var holder = Holder(x: 0)
    var job = touch.to[Work](holder: holder)
    job.start_now()
    show(f"got {job.retrieve()} holder {holder.x}")
    show("RESULT ok")
    return
"""
    if wait == "retrieve_list":
        return head + """
routine start()
    var holder = Holder(x: 0)
    var jobs = [touch.to[Work](holder: holder)]
    jobs[0].start_now()
    show(f"got {jobs[0].retrieve()} holder {holder.x}")
    show("RESULT ok")
    return
"""
    if wait == "gather":
        return head + """
routine start()
    var holder = Holder(x: 0)
    var jobs = [touch.to[Work](holder: holder), touch.to[Work](holder: holder)]
    jobs.start_now()
    show(f"got {jobs.gather().count()} holder {holder.x}")
    show("RESULT ok")
    return
"""
    if wait == "mailbox":
        return head + """
routine feed(holder: Holder, m: Mailbox[S64]) -> S64
    holder.x += 1
    m.add_last(value: holder.x)
    return 0

routine start()
    var holder = Holder(x: 0)
    var m = Mailbox[S64](capacity: 1)
    var job = feed.to[Work](holder: holder, m: m)
    job.start_now()
    show(f"got {m.remove_first()} holder {holder.x}")
    job.retrieve()
    show("RESULT ok")
    return
"""
    if wait == "channel":
        return head + """
routine send(holder: Holder, tx: Transmitter[S64]) -> S64
    holder.x += 1
    tx.transmit(item: holder.x)
    tx.close()
    return 0

routine start()
    var holder = Holder(x: 0)
    var (tx, rx) = make_shared_channel[S64](capacity: 1)
    var job = send.to[Work](holder: holder, tx: tx)
    job.start_now()
    var drain = rx.emitter()
    show(f"got {drain.emit()} holder {holder.x}")
    job.retrieve()
    show("RESULT ok")
    return
"""
    raise ValueError(wait)


CONFIG = """[package]
name = "{name}"

[target]
executable = "main"
mode = "debug"

[debug]
sanitize = "thread"
"""


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("outdir")
    ap.add_argument("--site", action="append")
    ap.add_argument("--dest", action="append")
    ap.add_argument("--value", action="append")
    ap.add_argument("--wait", action="append")
    ap.add_argument("--rounds", type=int, default=20000)
    ap.add_argument("--only", default="")
    ap.add_argument("--no-sanitize", action="store_true")
    a = ap.parse_args()
    cases = []
    for site in [] if a.wait is not None else (a.site or SITES):
        for dest in a.dest or DESTS:
            for value in a.value or VALUES:
                if value in ("variant", "nested") and dest in ORDERED_ELEMENTS:
                    # A variant has no hash and no order, and a record with a tuple member variable has none either
                    # (SF-S150), so a Set or Sorted container refuses them.
                    continue
                cases.append((f"{site}_{dest}_{value}", program(site, dest, value, a.rounds)))
    if a.wait is not None or not (a.site or a.dest or a.value):
        for wait in a.wait or WAITS:
            cases.append((f"wait_{wait}", wait_program(wait)))
    for name, text in cases:
        if a.only not in name:
            continue
        folder = os.path.join(a.outdir, name)
        os.makedirs(folder, exist_ok=True)
        with open(os.path.join(folder, "main.sf"), "w") as f:
            f.write(text)
        cfg = CONFIG.format(name=name)
        if a.no_sanitize:
            cfg = cfg.replace('\n[debug]\nsanitize = "thread"\n', "")
        with open(os.path.join(folder, "config.toml"), "w") as f:
            f.write(cfg)
        print(name)


if __name__ == "__main__":
    main()
