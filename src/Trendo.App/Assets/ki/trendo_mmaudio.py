"""Trendo: Geraeusch aus Text (oder passend zu einem Video) mit MMAudio (hkchengrex/MMAudio; Programm MIT, Modelle CC BY-NC 4.0).

Eigenes Startskript von Trendo, nutzt die Bibliothek von MMAudio. Speichert WAV ueber soundfile.
Aufruf: python trendo_mmaudio.py <repo> <ziel.wav> --prompt "..." [--duration 8] [--video datei.mp4] [--seed 42]
"""
import argparse
import os
import sys


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("repo")
    ap.add_argument("out")
    ap.add_argument("--prompt", default="")
    ap.add_argument("--negative", default="")
    ap.add_argument("--duration", type=float, default=8.0)
    ap.add_argument("--video", default=None)
    ap.add_argument("--seed", type=int, default=42)
    ap.add_argument("--steps", type=int, default=25)
    a = ap.parse_args()

    repo = os.path.abspath(a.repo)
    a.out = os.path.abspath(a.out)  # vor dem Ordnerwechsel festhalten
    if a.video:
        a.video = os.path.abspath(a.video)
    os.chdir(repo)  # MMAudio legt seine Modelle unter ./weights und ./ext_weights ab
    sys.path.insert(0, repo)

    import torch
    import soundfile as sf
    from mmaudio.eval_utils import all_model_cfg, generate, load_video
    from mmaudio.model.flow_matching import FlowMatching
    from mmaudio.model.networks import get_my_mmaudio
    from mmaudio.model.utils.features_utils import FeaturesUtils

    torch.backends.cuda.matmul.allow_tf32 = True
    cfg = all_model_cfg["large_44k_v2"]
    cfg.download_if_needed()
    seq = cfg.seq_cfg
    device = "cuda" if torch.cuda.is_available() else "cpu"
    dtype = torch.bfloat16 if device == "cuda" else torch.float32
    print(f"TRENDO geraet={device}", flush=True)

    with torch.inference_mode():
        net = get_my_mmaudio(cfg.model_name).to(device, dtype).eval()
        net.load_weights(torch.load(cfg.model_path, map_location=device, weights_only=True))
        feats = FeaturesUtils(tod_vae_ckpt=cfg.vae_path, synchformer_ckpt=cfg.synchformer_ckpt, enable_conditions=True,
                              mode=cfg.mode, bigvgan_vocoder_ckpt=cfg.bigvgan_16k_path, need_vae_encoder=False).to(device, dtype).eval()
        clip = sync = None
        duration = a.duration
        if a.video:
            info = load_video(a.video, duration)
            clip, sync, duration = info.clip_frames.unsqueeze(0), info.sync_frames.unsqueeze(0), info.duration_sec
        seq.duration = duration
        net.update_seq_lengths(seq.latent_seq_len, seq.clip_seq_len, seq.sync_seq_len)
        rng = torch.Generator(device=device)
        rng.manual_seed(a.seed)
        fm = FlowMatching(min_sigma=0, inference_mode="euler", num_steps=a.steps)
        audio = generate(clip, sync, [a.prompt], negative_text=[a.negative], feature_utils=feats, net=net, fm=fm, rng=rng, cfg_strength=4.5)
        wav = audio.float().cpu()[0].numpy()

    os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
    sf.write(a.out, wav.T if wav.ndim == 2 else wav, seq.sampling_rate)
    print(f"TRENDO fertig {duration:.1f} s", flush=True)


if __name__ == "__main__":
    main()
