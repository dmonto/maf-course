"""Runnable example for the corresponding Unit 15 syllabus section."""
from pathlib import Path
import json
import sys
import uuid

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
from shared import architecture_lab as lab
from shared.runtime import run_demo, stores, inspect, payload_for, save_json

def demonstrate(ctx):
    import ast
    import py_compile
    import sys
    source = Path(lab.__file__)
    tree = ast.parse(source.read_text(encoding='utf-8'))
    dependencies = sorted({node.names[0].name.split('.')[0] if isinstance(node, ast.Import)
                           else node.module.split('.')[0]
                           for node in ast.walk(tree)
                           if isinstance(node, (ast.Import, ast.ImportFrom)) and
                           (isinstance(node, ast.Import) or node.module)})
    external = sorted(set(dependencies) - set(sys.stdlib_module_names))
    assert external == []
    py_compile.compile(str(source), cfile=str(ctx.state / 'architecture_lab.pyc'), doraise=True)
    # This section deliberately uses the model-independent healthy path.
    with stores(ctx.state) as (producer, consumer):
        result = lab.assess(producer, lab.FixtureEvidence(), ctx.operation_id, 'email')
        lab.dispatch(producer, consumer)
        state = inspect(producer, consumer)
        assert state['inboxCount'] == 1 and state['reviewItems'] == []
    return {'standardLibraryImports': dependencies, 'externalPythonDependencies': external,
            'assessment': result, 'state': state, 'modelCalled': False}


if __name__ == '__main__':
    run_demo('v08', demonstrate)
