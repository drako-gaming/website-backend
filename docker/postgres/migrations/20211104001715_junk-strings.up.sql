create table junk_strings
(
    name varchar(30) not null primary key,
    value text not null
);

alter table junk_strings owner to drakoapi;
