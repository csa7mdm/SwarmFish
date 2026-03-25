FROM python:3.12-slim
WORKDIR /app
RUN pip install uv
COPY pyproject.toml .
RUN uv sync
RUN uv run python -m spacy download en_core_web_trf
COPY . .
EXPOSE 8001
HEALTHCHECK --interval=10s --retries=5 CMD curl -f http://localhost:8001/health || exit 1
CMD ["uv", "run", "uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8001"]
