import pytest
from app.services.extractor import extract, nlp

@pytest.mark.asyncio
async def test_extractor_runs_spacy():
    """
    Verify the spaCy pipeline extracts the target entity types on a fixture text.
    If the model is not installed (e.g. CI), we skip.
    """
    if not nlp:
        pytest.skip("spaCy model en_core_web_trf not loaded")
        
    text = "Tim Cook is the CEO of Apple Inc. Apple is located in Cupertino. The UN met today."
    
    entities, relationships = await extract(text)
    
    # We expect Tim Cook (PERSON), Apple Inc. / Apple (ORG), Cupertino (GPE), UN (ORG)
    ent_names = [e.name for e in entities]
    
    assert "Tim Cook" in ent_names
    # Depending on model exact boundaries, might be "Apple" or "Apple Inc."
    assert any("Apple" in name for name in ent_names)
    assert "Cupertino" in ent_names
    assert "UN" in ent_names
    
    # Check relationships are returned empty as per MVP
    assert relationships == []
