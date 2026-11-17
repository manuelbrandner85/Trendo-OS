"""Kleiner Rechner zum Ausprobieren."""

def addiere(a, b):
    return a + b


def teile(a, b):
    if b == 0:
        raise ValueError("Durch null kann man nicht teilen.")
    return a / b


def multipliziere(a, b):
    return a * b
