# File search engine

## About the project

File search engine is a simple project implementing different methods of searching using either NLP with vectorization or Elasticsearch.
The project uses two databases - SQLite and Elastic. They both store the same data, which leads to some redundancy but it's necessary to show the differences between the two methods.

## Installation and usage

This project required .NET 9 to compile and run. After compiling the project can be setup to run on IIS or better yet, IIS express provided by IDE.

After this the project does not require any additional setup. The databases are created automatically and filled with data from the provided files.
Those files can be found in the ``Files`` directory. When the program start the current working directory is retrieved and files are taken from ``CWD/Files/Default``. 
They are not copied to the output directory upon building.

## Setting up Elasticsearch

For the project to use Elasticsearch it needs 4 variables set in ``appsettings.json``:
```json
{
  "Elasticsearch": {
    "ElasticUrl": "http://localhost:9200",
    "ElasticUsername": "elastic",
    "ElasticPassword": "123123123",
    "ElasticFingerprint": "00:12:34:......:00"
  }
}
```

``ElasticUrl`` is the URL of the Elasticsearch instance. The default is ``http://localhost:9200``.

``ElasticUsername`` and ``ElasticPassword`` are the credentials for the Elasticsearch instance. The default is ``elastic`` and the password should be shown upon successful configuration of an instance.

``ElasticFingerprint`` is the fingerprint of the certificate used by the Elasticsearch instance.

It is recommended to use the dockerized version of Elasticsearch. More information can be found [here](https://www.elastic.co/guide/en/elasticsearch/reference/current/docker.html).

It is worth to note that you may encounter:
```
[1]: max virtual memory areas vm.max_map_count [65530] is too low, increase to at least [262144]
```
In this case run the following command in your docker WSL:
```sudo sysctl -w vm.max_map_count=262144```

