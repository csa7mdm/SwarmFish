import pytest
from app.services.embedder import chunk_text

def test_chunk_text_basic():
    text = " ".join([f"word{i}" for i in range(100)])
    
    # 512 size, 50 overlap -> for 100 words, it should be 1 chunk of size 100
    chunks = chunk_text(text, size=512, overlap=50)
    assert len(chunks) == 1
    assert len(chunks[0].split()) == 100

def test_chunk_text_multiple_chunks():
    text = " ".join([f"word{i}" for i in range(1000)])
    
    # words: 1000
    # chunk 1: 0-512
    # chunk 2: (512-50)=462 to 462+512=974
    # chunk 3: (974-50)=924 to 1000 (size 76)
    
    chunks = chunk_text(text, size=512, overlap=50)
    assert len(chunks) == 3
    assert len(chunks[0].split()) == 512
    assert len(chunks[1].split()) == 512
    assert len(chunks[2].split()) == 76
    
    # verify overlap (last 50 words of chunk 0 should be first 50 of chunk 1)
    chunk0_words = chunks[0].split()
    chunk1_words = chunks[1].split()
    assert chunk0_words[-50:] == chunk1_words[:50]
