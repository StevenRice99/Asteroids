uv venv --python 3.10.12
call .venv\Scripts\activate
uv pip install torch~=2.2.1 --index-url https://download.pytorch.org/whl/cu121
uv pip install mlagents==1.1.0
uv pip install "setuptools<70.0.0"
mlagents-learn --help