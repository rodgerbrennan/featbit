from setuptools import setup

setup(
    name='migrate-database',
    version='0.1',
    py_modules=['app'],
    install_requires=[
        'Click',
    ],
    entry_points='''
        [console_scripts]
        migrate-database=app.cli:migratedatabase
    ''',
)