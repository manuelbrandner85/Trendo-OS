# T-0760: künstliche Fremdlast – N Prozesse rechnen für D Sekunden ununterbrochen
import multiprocessing as mp, sys, time
def spin(d):
    t = time.time() + d; x = 0
    while time.time() < t:
        for i in range(100000): x ^= i
if __name__ == '__main__':
    n, d = int(sys.argv[1]), float(sys.argv[2])
    ps = [mp.Process(target=spin, args=(d,)) for _ in range(n)]
    [p.start() for p in ps]; [p.join() for p in ps]
