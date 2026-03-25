def chunk_text(text: str, size: int = 512, overlap: int = 50) -> list[str]:
    """
    Very basic word-based chunker approximating tokens.
    In a real app, use tiktoken or similar, but spec requests logic verification.
    """
    words = text.split()
    chunks = []
    
    if not words:
        return []
        
    i = 0
    while i < len(words):
        chunk_words = words[i:i + size]
        chunks.append(" ".join(chunk_words))
        
        if i + size >= len(words):
            break
            
        i += (size - overlap)
        
    return chunks

async def embed_batch(chunks: list[str]) -> list[list[float]]:
    """
    Stub for embedding generation. In real app, call OpenAI Embeddings API.
    For MVP pipeline logic, we return a mock 1536-dimensional vector for each chunk.
    """
    # Using mock embeddings to save API calls during testing
    return [[0.1] * 1536 for _ in chunks]
