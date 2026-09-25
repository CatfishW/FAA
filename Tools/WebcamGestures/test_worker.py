import importlib.util
import io
from pathlib import Path
import struct
from types import SimpleNamespace
import unittest

ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('worker',ROOT/'Assets/StreamingAssets/FAA/WebcamGestures/worker.py')
worker=importlib.util.module_from_spec(spec)
spec.loader.exec_module(worker)

class ProtocolTests(unittest.TestCase):
    def frame(self,seq=1,width=32,height=32,payload=None,magic=b'FWH1'):
        raw=bytes(width*height*3) if payload is None else payload
        return worker.HEADER.pack(magic,seq,width,height,len(raw))+raw
    def test_valid_rgb_roundtrip(self):
        seq,width,height,raw=worker.read_frame(io.BytesIO(self.frame()),0)
        self.assertEqual((seq,width,height,len(raw)),(1,32,32,3072))
    def test_clean_eof(self):
        with self.assertRaises(EOFError):worker.read_frame(io.BytesIO(),0)
    def test_truncated_frame(self):
        with self.assertRaises(ValueError):worker.read_frame(io.BytesIO(self.frame()[:-1]),0)
    def test_repeated_sequence(self):
        with self.assertRaises(ValueError):worker.read_frame(io.BytesIO(self.frame()),1)
    def test_invalid_dimensions(self):
        with self.assertRaises(ValueError):worker.read_frame(io.BytesIO(self.frame(width=1)),0)
    def test_invalid_magic(self):
        with self.assertRaises(ValueError):worker.read_frame(io.BytesIO(self.frame(magic=b'NOPE')),0)
    def test_invalid_payload_size(self):
        with self.assertRaises(ValueError):worker.read_frame(io.BytesIO(self.frame(payload=b'bad')),0)
    def test_incomplete_hand_rejected(self):
        self.assertIsNone(worker.summarize([],320,240))
    def test_nonfinite_hand_rejected(self):
        self.assertIsNone(worker.summarize([SimpleNamespace(x=float('nan'),y=.5)]*21,320,240))
    def test_degenerate_palm_rejected(self):
        self.assertIsNone(worker.summarize([SimpleNamespace(x=.5,y=.5)]*21,320,240))
    def test_pinch_distance_is_normalized_to_palm(self):
        p=[SimpleNamespace(x=.5,y=.5) for _ in range(21)]
        p[0]=SimpleNamespace(x=.5,y=.7);p[9]=SimpleNamespace(x=.5,y=.3)
        p[5]=SimpleNamespace(x=.4,y=.5);p[17]=SimpleNamespace(x=.6,y=.5)
        p[4]=SimpleNamespace(x=.5,y=.4);p[8]=SimpleNamespace(x=.51,y=.4)
        result=worker.summarize(p,320,240)
        self.assertAlmostEqual(result['palmSize'],.25)
        self.assertAlmostEqual(result['pinchRatio'],.04)

if __name__=='__main__':unittest.main()
